﻿namespace wave.clr.emit
{
    using System;
    using System.Data;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using exceptions;
    using insomnia;
    using runtime;
    using Serilog;

    public class ManaModuleBuilder : ManaModule, IBaker
    {
        private ILogger logger => Journal.Get(nameof(ManaModule));
        internal readonly ModuleBuilder clr_module;
        internal readonly AssemblyName clr_asm_name;
        internal readonly AssemblyBuilder clr_asm;
        internal ManaModuleBuilder(string name) : base(name)
        {
            clr_asm_name = new AssemblyName(name);
            clr_asm = AssemblyBuilder.DefineDynamicAssembly(clr_asm_name, AssemblyBuilderAccess.Run);
            clr_module = clr_asm.DefineDynamicModule(name);
            ManaTypeStorage.CLRModule = clr_module;
            InternSTD();
        }

        internal ManaModuleBuilder(string name, Version ver) : base(name, ver)
        {
            clr_asm_name = new AssemblyName(name);
            clr_asm = AssemblyBuilder.DefineDynamicAssembly(clr_asm_name, AssemblyBuilderAccess.Run);
            clr_module = clr_asm.DefineDynamicModule(name);
            ManaTypeStorage.CLRModule = clr_module;
            InternSTD();
        }


        private static void InternSTD()
        {
            ManaTypeStorage.Types[ManaCore.VoidClass.FullName] = typeof(void);
            ManaTypeStorage.Types[ManaCore.ObjectClass.FullName] = typeof(object);
            ManaTypeStorage.Types[ManaCore.ValueTypeClass.FullName] = typeof(ValueType);
            foreach (var @class in ManaCore.All) ManaTypeStorage.Intern(@class.AsType());
        }

        /// <summary>
        /// Define class by name.
        /// </summary>
        /// <remarks>
        /// 'assemblyName%global::namespace/className' - VALID
        /// <br/>
        /// 'global::namespace/className' - VALID
        /// <br/>
        /// 'namespace/className' - INVALID, need 'global::' prefix.
        /// <br/>
        /// 'className' - INVALID, need describe namespace.
        /// </remarks>
        /// <exception cref="IncompleteClassNameException">See 'remarks'.</exception>
        public ManaClassBuilder DefineClass(string classFullname, ClassFlags flags)
        {
            if (!classFullname.Contains("/"))
                throw new IncompleteClassNameException("Class name not contained namespace.");
            var typename = default(QualityTypeName);
            if (classFullname.Contains("%"))
            {
                if (!classFullname.StartsWith($"{Name}%"))
                    throw new IncompleteClassNameException($"Class name contains incorrect assembly name.");
                typename = new QualityTypeName(classFullname);
            }
            else
                typename = new QualityTypeName($"{Name}%{classFullname}");

            if (typename.TryGet(x => x.Namespace) is null)
                throw new IncompleteClassNameException($"Class name has incorrect format.");
            if (!typename.Namespace.StartsWith("global::"))
                throw new IncompleteClassNameException($"Class namespace not start with 'global::'.");

            return DefineClass(typename, flags);
        }
        /// <summary>
        /// Define class by name.
        /// </summary>
        public ManaClassBuilder DefineClass(QualityTypeName name, ClassFlags flags)
        {
            if (class_table.Any(x => x.FullName.Equals(name)))
                throw new DuplicateNameException($"Class '{name}' already defined.");

            var r = clr_module.DefineType(name.NameWithNS, flags.AsCLR() | TypeAttributes.Class);
            var clazz = new ManaClassBuilder(this, name, r);
            class_table.Add(clazz);
            clazz.Flags = flags;
            return clazz;
        }



        #region Implementation of IBaker

        public byte[] BakeByteArray()
        {
            throw new NotImplementedException();
        }

        public string BakeDebugString()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}