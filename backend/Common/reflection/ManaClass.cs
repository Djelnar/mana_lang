namespace mana.runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using reflection;
    using static ManaTypeCode;


    public class ManaClass : IEquatable<ManaClass>
    {
        public QualityTypeName FullName { get; set; }
        public string Name => FullName.Name;
        public string Path => FullName.Namespace;
        public ClassFlags Flags { get; set; }
        public ManaClass Parent { get; set; }
        public List<ManaField> Fields { get; } = new();
        public List<ManaMethod> Methods { get; set; } = new();
        public ManaTypeCode TypeCode { get; set; } = TYPE_CLASS;
        public bool IsPrimitive => TypeCode is not TYPE_CLASS and not TYPE_NONE;
        public bool IsValueType => IsPrimitive || this.Walk(x => x.Name == "ValueType");
        public ManaModule Owner { get; set; }
        public List<Aspect> Aspects { get; } = new();

        internal ManaClass(QualityTypeName name, ManaClass parent, ManaModule module)
        {
            this.FullName = name;
            this.Parent = parent;
            this.Owner = module;
        }
        internal ManaClass(ManaType type, ManaClass parent)
        {
            this.FullName = type.FullName;
            this.Parent = parent;
            this.TypeCode = type.TypeCode;
        }
        protected ManaClass() { }

        internal ManaMethod DefineMethod(string name, ManaClass returnType, MethodFlags flags, params ManaArgumentRef[] args)
        {
            var method = new ManaMethod(name, flags, returnType, this, args);
            method.Arguments.AddRange(args);

            if (Methods.Any(x => x.Name.Equals(method.Name)))
                return Methods.First(x => x.Name.Equals(method.Name));

            Methods.Add(method);
            return method;
        }

        public bool IsSpecial => Flags.HasFlag(ClassFlags.Special);
        public bool IsPublic => Flags.HasFlag(ClassFlags.Public);
        public bool IsPrivate => Flags.HasFlag(ClassFlags.Private);
        public bool IsAbstract => Flags.HasFlag(ClassFlags.Abstract);
        public bool IsStatic => Flags.HasFlag(ClassFlags.Static);
        public bool IsInternal => Flags.HasFlag(ClassFlags.Internal);

        public virtual ManaMethod GetDefaultDtor() => GetOrCreateTor("dtor()");
        public virtual ManaMethod GetDefaultCtor() => GetOrCreateTor("ctor()");

        public virtual ManaMethod GetStaticCtor() => GetOrCreateTor("type_ctor()", true);


        protected virtual ManaMethod GetOrCreateTor(string name, bool isStatic = false)
            => Methods.FirstOrDefault(x => x.IsStatic == isStatic && x.Name.Equals(name));

        public override string ToString()
            => $"{FullName}, {Flags} ({Parent?.FullName})";


        public ManaMethod FindMethod(string name, IEnumerable<ManaClass> args_types)
        {
            var result = this.Methods.FirstOrDefault(x =>
            {
                var nameHas = x.RawName.Equals(name);
                var argsHas = x.Arguments.Select(z => z.Type).SequenceEqual(args_types);

                return nameHas && argsHas;
            });
            return result ?? Parent?.FindMethod(name, args_types);
        }

        public ManaField? FindField(string name) =>
            this.Fields.FirstOrDefault(x => x.Name.Equals(name)) ?? Parent?.FindField(name);


        public ManaMethod? FindMethod(string name, Func<ManaMethod, bool> eq = null)
        {
            eq ??= s => s.RawName.Equals(name);

            foreach (var member in Methods)
            {
                if (eq(member))
                    return member;
            }

            return null;
        }

        #region Equality members

        public bool Equals(ManaClass other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(FullName, other.FullName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ManaClass)obj);
        }

        public override int GetHashCode()
            => HashCode.Combine(FullName, Parent);

        public static bool operator ==(ManaClass left, ManaClass right) => Equals(left, right);

        public static bool operator !=(ManaClass left, ManaClass right) => !Equals(left, right);

        #endregion
    }


    public static class TypeWalker
    {
        public static bool Walk(this ManaClass clazz, Func<ManaClass, bool> actor)
        {
            var target = clazz;

            while (target != null)
            {
                if (actor(target))
                    return true;

                target = target.Parent;
            }
            return false;
        }
    }
}
