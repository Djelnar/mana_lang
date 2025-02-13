namespace insomnia.compilation
{
    using mana.fs;
    using MoreLinq;
    using Spectre.Console;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using System.Threading;
    using ishtar;
    using mana;
    using mana.cmd;
    using mana.exceptions;
    using mana.ishtar.emit;
    using mana.extensions;
    using mana.pipes;
    using mana.project;
    using mana.runtime;
    using mana.stl;
    using mana.syntax;
    using static mana.runtime.ManaTypeCode;
    using static Spectre.Console.AnsiConsole;
    using Console = System.Console;

    public class Compiler
    {

        public static Compiler Process(FileInfo[] entity, ManaProject project, CompileSettings flags)
        {
            var c = new Compiler(project, flags);

            return Status()
                .Spinner(Spinner.Known.Dots8Bit)
                .Start("Processing...", ctx =>
                {
                    c.Status = ctx;
                    try
                    {
                        c.ProcessFiles(entity);
                    }
                    catch (Exception e)
                    {
                        MarkupLine("failed compilation.");
                        WriteException(e);
                    }
                    return c;
                });
        }

        public Compiler(ManaProject project, CompileSettings flags)
        {
            _flags = flags;
            Project = project;
            var pack = project.SDK.GetPackByAlias(project.Runtime);
            resolver.AddSearchPath(new(project.WorkDir));
            resolver.AddSearchPath(new(project.SDK.GetFullPath(pack)));
        }

        internal ManaProject Project { get; set; }

        internal readonly CompileSettings _flags;
        internal readonly ManaSyntax syntax = new();
        internal readonly AssemblyResolver resolver = new ();
        internal readonly Dictionary<FileInfo, string> Sources = new ();
        internal readonly Dictionary<FileInfo, DocumentDeclaration> Ast = new();
        internal StatusContext Status;
        public readonly List<string> warnings = new ();
        public readonly List<string> errors = new ();
        internal ManaModuleBuilder module;
        internal GeneratorContext Context;

        private void ProcessFiles(FileInfo[] files)
        {
            if (_flags.IsNeedDebuggerAttach)
            {
                while (!Debugger.IsAttached)
                {
                    Status.ManaStatus($"[green]Waiting debugger[/]...");
                    Thread.Sleep(400);
                }
            }
            var deps = new List<ManaModule>();
            foreach (var (name, version) in Project.Packages)
            {
                Status.ManaStatus($"Resolve [grey]'{name}, {version}'[/]...");
                deps.Add(resolver.ResolveDep(name, version.Version, deps));
            }
            foreach (var file in files)
            {
                Status.ManaStatus($"Read [grey]'{file.Name}'[/]...");
                Sources.Add(file, File.ReadAllText(file.FullName));
            }

            foreach (var (key, value) in Sources)
            {
                Status.ManaStatus($"Compile [grey]'{key.Name}'[/]...");
                try
                {
                    var result = syntax.CompilationUnit.ParseMana(value);
                    result.FileEntity = key;
                    result.SourceText = value;
                    // apply root namespace into includes
                    result.Includes.Add($"global::{result.Name}");
                    Ast.Add(key, result);
                }
                catch (ManaParseException e)
                {
                    errors.Add($"[red bold]{e.Message.Trim().EscapeMarkup()}[/] \n\t" +
                               $"at '[orange bold]{e.Position.Line} line, {e.Position.Column} column[/]' \n\t" +
                               $"in '[orange bold]{key}[/]'.");
                }
            }

            Context = new GeneratorContext();

            module = new ManaModuleBuilder(Project.Name);

            Context.Module = module;
            Context.Module.Deps.AddRange(deps);

            Ast.Select(x => (x.Key, x.Value))
                .Pipe(x => Status.ManaStatus($"Linking [grey]'{x.Key.Name}'[/]..."))
                .SelectMany(x => LinkClasses(x.Value))
                .ToList()
                .Pipe(LinkMetadata)
                .Select(x => (LinkMethods(x), x))
                .ToList()
                .Pipe(x => x.Item1.Transition(
                    methods => methods.ForEach(GenerateBody),
                    fields => fields.ForEach(GenerateField)))
                .Select(x => x.x)
                .Pipe(GenerateCtor)
                .Pipe(GenerateStaticCtor)
                .Consume();
            errors.AddRange(Context.Errors);
            if (errors.Count == 0)
                PipelineRunner.Run(this);
            MarkupLine($"[blue]INF[/]: Result assembly [orange]'{module.Name}, {module.Version}'[/].");
            if (_flags.PrintResultType)
            {
                var table = new Table();
                table.AddColumn(new TableColumn("Type").Centered());
                table.Border(TableBorder.Rounded);
                foreach (var @class in module.class_table)
                    table.AddRow(new Markup($"[blue]{@class.FullName.NameWithNS}[/]"));
                AnsiConsole.Render(table);
            }
        }

        public void WriteOutput(ManaModuleBuilder builder)
        {
            var dirInfo = new DirectoryInfo(Path.Combine(Project.WorkDir, "bin"));

            if (!dirInfo.Exists)
                dirInfo.Create();
            else
                dirInfo.EnumerateFiles("*.*", SearchOption.AllDirectories).ForEach(x => x.Delete());


            var asm_file = new FileInfo(Path.Combine(dirInfo.FullName, $"{Project.Name}.wll"));
            var wil_file = new FileInfo(Path.Combine(dirInfo.FullName, $"{Project.Name}.wvil.bin"));

            var wil_data = builder.BakeByteArray();


            var asm = new IshtarAssembly(builder);

            IshtarAssembly.WriteTo(asm, asm_file.FullName);

            File.WriteAllBytes(wil_file.FullName, wil_data);
        }


        public List<(ClassBuilder clazz, ClassDeclarationSyntax member)> LinkClasses(DocumentDeclaration doc)
        {
            var classes = new List<(ClassBuilder clazz, ClassDeclarationSyntax member)>();

            foreach (var member in doc.Members)
            {
                if (member is ClassDeclarationSyntax clazz)
                {
                    Status.ManaStatus($"Regeneration class [grey]'{clazz.Identifier}'[/]");
                    clazz.OwnerDocument = doc;
                    var result = CompileClass(clazz, doc);
                    Context.Classes.Add(result.FullName, result);
                    classes.Add((result, clazz));
                }
                else
                    warnings.Add($"[grey]Member[/] [yellow underline]'{member.GetType().Name}'[/] [grey]is not supported.[/]");
            }

            return classes;
        }

        public ClassBuilder CompileClass(ClassDeclarationSyntax member, DocumentDeclaration doc)
        {
            CompileAnnotation(member, doc);
            if (member.IsForwardedType)
            {
                var result = ManaCore.All.
                    FirstOrDefault(x => x.FullName.Name.Equals(member.Identifier.ExpressionString));

                if (result is not null)
                {
                    var clz = new ClassBuilder(module, result);
                    module.class_table.Add(clz);

                    clz.Includes.AddRange(doc.Includes);
                    return clz;
                }

                throw new ForwardedTypeNotDefinedException(member.Identifier.ExpressionString);
            }
            return module.DefineClass($"global::{doc.Name}/{member.Identifier.ExpressionString}").WithIncludes(doc.Includes);
        }
        public void CompileAnnotation(FieldDeclarationSyntax field, DocumentDeclaration doc) =>
            CompileAnnotation(field.Annotations, x =>
                $"aspect/{x.AnnotationKind}/class/{field.OwnerClass.Identifier}/field/{field.Field.Identifier}.", doc);

        public void CompileAnnotation(MethodDeclarationSyntax method, DocumentDeclaration doc) =>
            CompileAnnotation(method.Annotations, x =>
                $"aspect/{x.AnnotationKind}/class/{method.OwnerClass.Identifier}/method/{method.Identifier}.", doc);

        public void CompileAnnotation(ClassDeclarationSyntax clazz, DocumentDeclaration doc) =>
            CompileAnnotation(clazz.Annotations, x =>
                $"aspect/{x.AnnotationKind}/class/{clazz.Identifier}.", doc);

        private void CompileAnnotation(
            List<AnnotationSyntax> annotations,
            Func<AnnotationSyntax, string> nameGenerator,
            DocumentDeclaration doc)
        {
            foreach (var annotation in annotations.TrimNull().Where(annotation => annotation.Args.Length != 0))
            {
                foreach (var (exp, index) in annotation.Args.Select((x, y) => (x, y)))
                {
                    if (exp.CanOptimizationApply())
                    {
                        var optimized = exp.ForceOptimization();
                        var converter = optimized.GetTypeCode().GetConverter();
                        module.WriteToConstStorage($"{nameGenerator(annotation)}_{index}",
                            converter(optimized.ExpressionString));
                    }
                    else
                    {
                        var diff_err = annotation.Transform.DiffErrorFull(doc);
                        errors.Add($"[red bold]Annotations require compile-time constant.[/] \n\t" +
                                   $"at '[orange bold]{annotation.Transform.pos.Line} line, {annotation.Transform.pos.Column} column[/]' \n\t" +
                                   $"in '[orange bold]{doc.FileEntity}[/]'." +
                                   $"{diff_err}");
                    }
                }
            }
        }

        public void LinkMetadata((ClassBuilder @class, ClassDeclarationSyntax member) x)
        {
            var (@class, member) = x;
            var doc = member.OwnerDocument;
            @class.Flags = GenerateClassFlags(member);

            var owner = member.Inheritances.FirstOrDefault();

            // ignore core base types
            if (member.Identifier.ExpressionString is "Object" or "ValueType") // TODO
                return;

            // TODO set for struct ValueType owner
            owner ??= new TypeSyntax(new IdentifierExpression("Object"));

            @class.Parent = FetchType(owner, doc);
        }
        public (
            List<(MethodBuilder method, MethodDeclarationSyntax syntax)> methods,
            List<(ManaField field, FieldDeclarationSyntax syntax)>)
            LinkMethods((ClassBuilder @class, ClassDeclarationSyntax member) x)
        {
            var (@class, clazzSyntax) = x;
            var doc = clazzSyntax.OwnerDocument;
            var methods = new List<(MethodBuilder method, MethodDeclarationSyntax syntax)>();
            var fields = new List<(ManaField field, FieldDeclarationSyntax syntax)>();
            foreach (var member in clazzSyntax.Members)
            {
                switch (member)
                {
                    case IPassiveParseTransition transition when member.IsBrokenToken:
                        var e = transition.Error;
                        var pos = member.Transform.pos;
                        var err_line = member.Transform.DiffErrorFull(doc);
                        errors.Add($"[red bold]{e.Message.Trim().EscapeMarkup()}, expected {e.FormatExpectations().EscapeMarkup().EscapeArgumentSymbols()}[/] \n\t" +
                                   $"at '[orange bold]{pos.Line} line, {pos.Column} column[/]' \n\t" +
                                   $"in '[orange bold]{doc.FileEntity}[/]'." +
                                   $"{err_line}");
                        break;
                    case MethodDeclarationSyntax method:
                        Status.ManaStatus($"Regeneration method [grey]'{method.Identifier}'[/]");
                        method.OwnerClass = clazzSyntax;
                        methods.Add(CompileMethod(method, @class, doc));
                        break;
                    case FieldDeclarationSyntax field:
                        Status.ManaStatus($"Regeneration field [grey]'{field.Field.Identifier}'[/]");
                        field.OwnerClass = clazzSyntax;
                        fields.Add(CompileField(field, @class, doc));
                        break;
                    default:
                        warnings.Add($"[grey]Member[/] '[yellow underline]{member.GetType().Name}[/]' [grey]is not supported.[/]");
                        break;
                }
            }
            return (methods, fields);
        }

        public (MethodBuilder method, MethodDeclarationSyntax syntax)
            CompileMethod(MethodDeclarationSyntax member, ClassBuilder clazz, DocumentDeclaration doc)
        {
            CompileAnnotation(member, doc);

            var retType = FetchType(member.ReturnType, doc);

            if (retType is null)
                return default;

            var args = GenerateArgument(member, doc);

            var method = clazz.DefineMethod(member.Identifier.ExpressionString, GenerateMethodFlags(member), retType, args);

            method.Owner = clazz;

            return (method, member);
        }

        public (ManaField field, FieldDeclarationSyntax member)
            CompileField(FieldDeclarationSyntax member, ClassBuilder clazz, DocumentDeclaration doc)
        {
            CompileAnnotation(member, doc);

            var fieldType = FetchType(member.Type, doc);

            if (fieldType is null)
                return default;

            var field = clazz.DefineField(member.Field.Identifier.ExpressionString, GenerateFieldFlags(member), fieldType);
            return (field, member);
        }

        public void GenerateCtor((ClassBuilder @class, ClassDeclarationSyntax member) x)
        {
            var (@class, member) = x;
            Context.Document = member.OwnerDocument;
            Status.ManaStatus($"Regenerate default ctor for [grey]'{member.Identifier}'[/].");
            var ctor = @class.GetDefaultCtor() as MethodBuilder;
            var doc = member.OwnerDocument;

            if (ctor is null)
            {
                errors.Add($"[red bold]Class/struct '{@class.Name}' has problem with generate default ctor.[/] \n\t" +
                           $"Please report the problem into 'https://github.com/0xF6/mana_lang/issues'." +
                           $"in '[orange bold]{doc.FileEntity}[/]'.");
                return;
            }

            Context.CurrentMethod = ctor;

            var gen = ctor.GetGenerator();

            gen.StoreIntoMetadata("context", Context);


            var pctor = @class.Parent?.GetDefaultCtor();

            if (pctor is not null) // for Object, ValueType
                gen.Emit(OpCodes.CALL, pctor); // call parent ctor
            var pregen = new List<(ExpressionSyntax exp, ManaField field)>();


            foreach (var field in @class.Fields)
            {
                if (field.IsStatic)
                    continue;
                if (field.IsLiteral)
                    continue;
                var stx = member.Fields
                    .SingleOrDefault(x => x.Field.Identifier.ExpressionString.Equals(field.Name));
                if (stx is null)
                {
                    errors.Add($"[red bold]Field '{field.Name}' in class/struct '{@class.Name}' has undefined.[/] \n\t" +
                               $"in '[orange bold]{doc.FileEntity}[/]'.");
                    continue;
                }
                pregen.Add((stx.Field.Expression, field));
            }

            foreach (var (exp, field) in pregen)
            {
                if (exp is null)
                    gen.Emit(OpCodes.LDNULL);
                else
                    gen.EmitExpression(exp);
                gen.Emit(OpCodes.STF, field);
            }
        }

        public void GenerateStaticCtor((ClassBuilder @class, ClassDeclarationSyntax member) x)
        {
            var (@class, member) = x;
            Status.ManaStatus($"Regenerate static ctor for [grey]'{member.Identifier}'[/].");
            var ctor = @class.GetStaticCtor() as MethodBuilder;
            var doc = member.OwnerDocument;

            if (ctor is null)
            {
                errors.Add($"[red bold]Class/struct '{@class.Name}' has problem with generate static ctor.[/] \n\t" +
                           $"Please report the problem into 'https://github.com/0xF6/mana_lang/issues'." +
                           $"in '[orange bold]{doc.FileEntity}[/]'.");
                return;
            }
            Context.CurrentMethod = ctor;

            var gen = ctor.GetGenerator();

            gen.StoreIntoMetadata("context", Context);

            var pregen = new List<(ExpressionSyntax exp, ManaField field)>();

            foreach (var field in @class.Fields)
            {
                // skip non-static field,
                // they do not need to be initialized in the static constructor
                if (!field.IsStatic)
                    continue;
                var stx = member.Fields
                    .SingleOrDefault(x => x.Field.Identifier.ExpressionString.Equals(field.Name));
                if (stx is null)
                {
                    errors.Add($"[red bold]Field '{field.Name}' in class/struct '{@class.Name}' has undefined.[/] \n\t" +
                               $"in '[orange bold]{doc.FileEntity}[/]'.");
                    continue;
                }
                pregen.Add((stx.Field.Expression, field));
            }

            foreach (var (exp, field) in pregen)
            {
                if (exp is null)
                    // value_type can also have a NULL value
                    gen.Emit(OpCodes.LDNULL);
                else
                    gen.EmitExpression(exp);
                gen.Emit(OpCodes.STSF, field);
            }
        }

        public void GenerateBody((MethodBuilder method, MethodDeclarationSyntax member) t)
        {
            if (t == default)
            {
                errors.Add($"[red bold]Unknown error[/] in [italic]GenerateBody(...);[/]");
                return;
            }
            var (method, member) = t;

            foreach (var pr in member.Body.Statements.SelectMany(x => x.ChildNodes.Concat(new[] { x })))
                AnalyzeStatement(pr, member);


            var generator = method.GetGenerator();
            Context.Document = member.OwnerClass.OwnerDocument;
            Context.CurrentMethod = method;
            Context.CreateScope();
            generator.StoreIntoMetadata("context", Context);

            foreach (var statement in member.Body.Statements)
            {
                try
                {
                    generator.EmitStatement(statement);
                }
                catch (Exception e)
                {
                    PrintError($"[red bold]{e.Message.EscapeMarkup()}[/] in [italic]GenerateBody(...);[/]", statement, Context.Document);
                }
            }
            // fucking shit fucking
            // VM needs the end-of-method notation, which is RETURN.
            // but in case of the VOID method, user may not write it
            // and i didnt think of anything smarter than checking last OpCode
            if (!generator._opcodes.Any() && method.ReturnType.TypeCode == TYPE_VOID)
                generator.Emit(OpCodes.RET);
            if (generator._opcodes.Any() && generator._opcodes.Last() != OpCodes.RET.Value && method.ReturnType.TypeCode == TYPE_VOID)
                generator.Emit(OpCodes.RET);
        }

        private void AnalyzeStatement(BaseSyntax statement, MethodDeclarationSyntax member)
        {
            if (statement is not IPassiveParseTransition { IsBrokenToken: true } transition)
                return;
            var doc = member.OwnerClass.OwnerDocument;
            var pos = statement.Transform.pos;
            var e = transition.Error;
            var diff_err = statement.Transform.DiffErrorFull(doc);
            errors.Add($"[red bold]{e.Message.Trim().EscapeMarkup()}, expected {e.FormatExpectations().EscapeMarkup().EscapeArgumentSymbols()}[/] \n\t" +
                       $"at '[orange bold]{pos.Line} line, {pos.Column} column[/]' \n\t" +
                       $"in '[orange bold]{doc.FileEntity}[/]'." +
                       $"{diff_err}");
        }
        public void GenerateField((ManaField field, FieldDeclarationSyntax member) t)
        {
            if (t == default)
            {
                errors.Add($"[red bold]Unknown error[/] in [italic]GenerateBody(...);[/]");
                return;
            }


            var (field, member) = t;
            var doc = member.OwnerClass.OwnerDocument;

            // skip uninited fields
            if (member.Field.Expression is null)
                return;

            // validate type compatible
            if (member.Field.Expression is LiteralExpressionSyntax literal)
            {
                if (literal is NumericLiteralExpressionSyntax numeric)
                {
                    if (!field.FieldType.TypeCode.CanImplicitlyCast(numeric))
                    {
                        var diff_err = literal.Transform.DiffErrorFull(doc);

                        var value = numeric.GetTypeCode();
                        var variable = member.Type.Identifier;
                        var variable1 = field.FieldType.TypeCode;

                        errors.Add(
                            $"[red bold]Cannot implicitly convert type[/] " +
                            $"'[purple underline]{numeric.GetTypeCode().AsClass().Name}[/]' to " +
                            $"'[purple underline]{field.FieldType.Name}[/]'.\n\t" +
                            $"at '[orange bold]{numeric.Transform.pos.Line} line, {numeric.Transform.pos.Column} column[/]' \n\t" +
                            $"in '[orange bold]{doc.FileEntity}[/]'." +
                            $"{diff_err}");
                    }
                }
                else if (literal.GetTypeCode() != field.FieldType.TypeCode)
                {
                    var diff_err = literal.Transform.DiffErrorFull(doc);
                    errors.Add(
                        $"[red bold]Cannot implicitly convert type[/] " +
                        $"'[purple underline]{literal.GetTypeCode().AsClass().Name}[/]' to " +
                        $"'[purple underline]{member.Type.Identifier}[/]'.\n\t" +
                        $"at '[orange bold]{literal.Transform.pos.Line} line, {literal.Transform.pos.Column} column[/]' \n\t" +
                        $"in '[orange bold]{doc.FileEntity}[/]'." +
                        $"{diff_err}");
                }
            }

            var clazz = field.Owner;

            if (member.Modifiers.Any(x => x.ModificatorKind == ModificatorKind.Const))
            {
                var assigner = member.Field.Expression;

                if (assigner is NewExpressionSyntax)
                {
                    var diff_err = assigner.Transform.DiffErrorFull(doc);
                    errors.Add(
                        $"[red bold]The expression being assigned to[/] '[purple underline]{member.Field.Identifier}[/]' [red bold]must be constant[/]. \n\t" +
                        $"at '[orange bold]{assigner.Transform.pos.Line} line, {assigner.Transform.pos.Column} column[/]' \n\t" +
                        $"in '[orange bold]{doc.FileEntity}[/]'." +
                        $"{diff_err}");
                    return;
                }

                try
                {
                    var converter = field.GetConverter();

                    if (assigner is UnaryExpressionSyntax { OperatorType: ExpressionType.Negate } negate)
                        module.WriteToConstStorage(field.FullName, converter($"-{negate.ExpressionString.Trim('(', ')')}")); // shit
                    else
                        module.WriteToConstStorage(field.FullName, converter(assigner.ExpressionString));
                }
                catch (ValueWasIncorrectException e)
                {
                    throw new MaybeMismatchTypeException(field, e);
                }
            }
        }
        
        private ManaClass FetchType(TypeSyntax typename, DocumentDeclaration doc)
        {
            var retType = module.TryFindType(typename.Identifier.ExpressionString, doc.Includes);

            if (retType is null)
                errors.Add($"[red bold]Cannot resolve type[/] '[purple underline]{typename.Identifier}[/]' \n\t" +
                           $"at '[orange bold]{typename.Transform.pos.Line} line, {typename.Transform.pos.Column} column[/]' \n\t" +
                           $"in '[orange bold]{doc.FileEntity}[/]'.");
            return retType;
        }

        private ManaArgumentRef[] GenerateArgument(MethodDeclarationSyntax method, DocumentDeclaration doc)
        {
            if (method.Parameters.Count == 0)
                return Array.Empty<ManaArgumentRef>();
            return method.Parameters.Select(parameter => new ManaArgumentRef
            { Type = FetchType(parameter.Type, doc), Name = parameter.Identifier.ExpressionString })
                .ToArray();
        }
        private ClassFlags GenerateClassFlags(ClassDeclarationSyntax clazz)
        {
            var flags = (ClassFlags) 0;

            var annotations = clazz.Annotations;
            var mods = clazz.Modifiers;

            foreach (var annotation in annotations)
            {
                var kind = annotation.AnnotationKind;
                switch (kind)
                {
                    case ManaAnnotationKind.Getter:
                    case ManaAnnotationKind.Setter:
                    case ManaAnnotationKind.Virtual:
                        PrintError(
                            $"Cannot apply [orange bold]annotation[/] [red bold]{kind}[/] to [orange]'{clazz.Identifier}'[/] " +
                            $"class/struct/interface declaration.",
                            annotation, clazz.OwnerDocument);
                        continue;
                    case ManaAnnotationKind.Special:
                        flags |= ClassFlags.Special;
                        continue;
                    case ManaAnnotationKind.Native:
                        continue;
                    case ManaAnnotationKind.Readonly when !clazz.IsStruct:
                        PrintError(
                            $"[orange bold]Annotation[/] [red bold]{kind}[/] can only be applied to a structure declaration.",
                            annotation, clazz.OwnerDocument);
                        continue;
                    case ManaAnnotationKind.Readonly when clazz.IsStruct:
                        // TODO
                        continue;
                    case ManaAnnotationKind.Forwarded:
                        continue;
                    default:
                        PrintError(
                            $"In [orange]'{clazz.Identifier}'[/] class/struct/interface [red bold]{kind}[/] " +
                            $"is not supported [orange bold]annotation[/].",
                            annotation, clazz.OwnerDocument);
                        continue;
                }
            }

            foreach (var mod in mods)
            {
                switch (mod.ModificatorKind.ToString().ToLower())
                {
                    case "public":
                        flags |= ClassFlags.Public;
                        continue;
                    case "private":
                        flags |= ClassFlags.Private;
                        continue;
                    case "static":
                        flags |= ClassFlags.Static;
                        continue;
                    case "internal":
                        flags |= ClassFlags.Internal;
                        continue;
                    case "abstract":
                        flags |= ClassFlags.Abstract;
                        continue;
                    case "extern":
                        continue;
                    default:
                        errors.Add(
                            $"In [orange]'{clazz.Identifier}'[/] class/struct/interface [red bold]{mod}[/] is not supported [orange bold]modificator[/].");
                        continue;
                }
            }

            return flags;
        }

        private FieldFlags GenerateFieldFlags(FieldDeclarationSyntax field)
        {
            var flags = (FieldFlags)0;

            var annotations = field.Annotations;
            var mods = field.Modifiers;

            foreach (var annotation in annotations)
            {
                switch (annotation.AnnotationKind)
                {
                    case ManaAnnotationKind.Virtual:
                        flags |= FieldFlags.Virtual;
                        continue;
                    case ManaAnnotationKind.Special:
                        flags |= FieldFlags.Special;
                        continue;
                    case ManaAnnotationKind.Native:
                        continue;
                    case ManaAnnotationKind.Readonly:
                        flags |= FieldFlags.Readonly;
                        continue;
                    case ManaAnnotationKind.Getter:
                    case ManaAnnotationKind.Setter:
                        //errors.Add($"In [orange]'{field.Field.Identifier}'[/] field [red bold]{kind}[/] is not supported [orange bold]annotation[/].");
                        continue;
                    default:
                        PrintError(
                            $"In [orange]'{field.Field.Identifier}'[/] field [red bold]{annotation.AnnotationKind}[/] " +
                            $"is not supported [orange bold]annotation[/].",
                            annotation, field.OwnerClass.OwnerDocument);
                        continue;
                }
            }

            foreach (var mod in mods)
            {
                switch (mod.ModificatorKind.ToString().ToLower())
                {
                    case "private":
                        continue;
                    case "public":
                        flags |= FieldFlags.Public;
                        continue;
                    case "static":
                        flags |= FieldFlags.Static;
                        continue;
                    case "protected":
                        flags |= FieldFlags.Protected;
                        continue;
                    case "internal":
                        flags |= FieldFlags.Internal;
                        continue;
                    case "override":
                        flags |= FieldFlags.Override;
                        continue;
                    case "abstract":
                        flags |= FieldFlags.Abstract;
                        continue;
                    case "const":
                        flags |= FieldFlags.Literal;
                        continue;
                    default:
                        PrintError(
                            $"In [orange]'{field.Field.Identifier}'[/] field [red bold]{mod.ModificatorKind}[/] " +
                            $"is not supported [orange bold]modificator[/].",
                            mod, field.OwnerClass.OwnerDocument);
                        continue;
                }
            }


            //if (flags.HasFlag(FieldFlags.Private) && flags.HasFlag(MethodFlags.Public))
            //    errors.Add($"Modificator [red bold]public[/] cannot be combined with [red bold]private[/] in [orange]'{field.Field.Identifier}'[/] field.");


            return flags;
        }

        private void PrintError(string text, BaseSyntax posed, DocumentDeclaration doc)
        {
            if (posed is { Transform: null })
            {
                errors.Add($"INTERNAL ERROR: TOKEN '{posed.GetType().Name}' HAS INCORRECT TRANSFORM POSITION.");
                return;
            }

            var strBuilder = new StringBuilder();

            strBuilder.Append($"{text.EscapeArgumentSymbols()}\n");



            if (posed is not null)
            {
                strBuilder.Append(
                    $"\tat '[orange bold]{posed.Transform.pos.Line} line, {posed.Transform.pos.Column} column[/]' \n");
            }

            if (doc is not null)
            {
                strBuilder.Append(
                    $"\tin '[orange bold]{doc.FileEntity}[/]'.");
            }

            if (posed is not null && doc is not null)
            {
                var diff_err = posed.Transform.DiffErrorFull(doc);
                strBuilder.Append($"\t\t{diff_err}");
            }

            errors.Add(strBuilder.ToString());
        }

        private MethodFlags GenerateMethodFlags(MethodDeclarationSyntax method)
        {
            var flags = (MethodFlags)0;

            var annotations = method.Annotations;
            var mods = method.Modifiers;

            foreach (var annotation in annotations)
            {
                switch (annotation.AnnotationKind)
                {
                    case ManaAnnotationKind.Virtual:
                        flags |= MethodFlags.Virtual;
                        continue;
                    case ManaAnnotationKind.Special:
                        flags |= MethodFlags.Special;
                        continue;
                    case ManaAnnotationKind.Native:
                        continue;
                    case ManaAnnotationKind.Readonly:
                    case ManaAnnotationKind.Getter:
                    case ManaAnnotationKind.Setter:
                        PrintError(
                            $"In [orange]'{method.Identifier}'[/] method [red bold]{annotation.AnnotationKind}[/] " +
                            $"is not supported [orange bold]annotation[/].",
                            annotation, method.OwnerClass.OwnerDocument);
                        continue;
                    default:
                        PrintError(
                            $"In [orange]'{method.Identifier}'[/] method [red bold]{annotation.AnnotationKind}[/] " +
                            $"is not supported [orange bold]annotation[/].",
                            annotation, method.OwnerClass.OwnerDocument);
                        continue;
                }
            }

            foreach (var mod in mods)
            {
                switch (mod.ModificatorKind.ToString().ToLower())
                {
                    case "public":
                        flags |= MethodFlags.Public;
                        continue;
                    case "extern":
                        flags |= MethodFlags.Extern;
                        continue;
                    case "private":
                        flags |= MethodFlags.Private;
                        continue;
                    case "static":
                        flags |= MethodFlags.Static;
                        continue;
                    case "protected":
                        flags |= MethodFlags.Protected;
                        continue;
                    case "internal":
                        flags |= MethodFlags.Internal;
                        continue;
                    default:
                        PrintError(
                            $"In [orange]'{method.Identifier}'[/] method [red bold]{mod}[/] " +
                            $"is not supported [orange bold]modificator[/].",
                            mod, method.OwnerClass.OwnerDocument);
                        continue;
                }
            }


            if (flags.HasFlag(MethodFlags.Private) && flags.HasFlag(MethodFlags.Public))
                PrintError(
                    $"Modificator [red bold]public[/] cannot be combined with [red bold]private[/] " +
                    $"in [orange]'{method.Identifier}'[/] method.",
                    method.ReturnType, method.OwnerClass.OwnerDocument);


            return flags;
        }
    }

    public static class ManaStatusContextEx
    {
        /// <summary>Sets the status message.</summary>
        /// <param name="context">The status context.</param>
        /// <param name="status">The status message.</param>
        /// <returns>The same instance so that multiple calls can be chained.</returns>
        public static StatusContext ManaStatus(this StatusContext context, string status)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            Thread.Sleep(10); // so, i need it :(
            context.Status = status;
            return context;
        }
    }
}
