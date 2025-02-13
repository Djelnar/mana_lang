﻿namespace wave.llvm.emit
{
    using LLVMSharp;
    using runtime;

    public class MethodBuilder : ManaMethod, IBaker
    {
        internal readonly ClassBuilder classBuilder;
        internal ManaModuleBuilder moduleBuilder 
            => classBuilder?.moduleBuilder;


        private LLVMValueRef @ref;


        internal MethodBuilder(ClassBuilder clazz, string name, ManaType returnType, params ManaArgumentRef[] args) 
            : base(name, 0, returnType, clazz, args)
        {
            classBuilder = clazz;
            clazz.moduleBuilder.InternString(Name);
            @ref = LLVM.AddFunction(moduleBuilder.@ref, name, returnType.AsLLVM());
        }

        #region Implementation of IBaker

        public byte[] BakeByteArray()
        {
            throw new System.NotImplementedException();
        }

        public string BakeDebugString()
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}