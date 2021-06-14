namespace ishtar
{
    using System.Runtime.InteropServices;
    using static mana.runtime.ManaTypeCode;
    public static unsafe class IshtarMarshal
    {
        public static IshtarObject* ToIshtarObject(string str, CallFrame frame = null)
        {
            var arg = IshtarGC.AllocObject(TYPE_STRING.AsRuntimeClass());
            var clazz = IshtarUnsafe.AsRef<RuntimeIshtarClass>(arg->clazz);
            arg->vtable[clazz.Field["!!value"].vtable_offset] = StringStorage.Intern(str);
            return arg;
        }
        public static IshtarObject* ToIshtarObject(int dotnet_value, CallFrame frame = null)
        {
            var obj = IshtarGC.AllocObject(TYPE_I4.AsRuntimeClass());
            var clazz = IshtarUnsafe.AsRef<RuntimeIshtarClass>(obj->clazz);
            obj->vtable[clazz.Field["!!value"].vtable_offset] = (int*)dotnet_value;

            return obj;
        }

        public static IshtarObject* ToIshtarObject(bool dotnet_value, CallFrame frame = null)
        {
            var obj = IshtarGC.AllocObject(TYPE_BOOLEAN.AsRuntimeClass());
            var clazz = IshtarUnsafe.AsRef<RuntimeIshtarClass>(obj->clazz);
            obj->vtable[clazz.Field["!!value"].vtable_offset] = (int*)(dotnet_value ? 1 : 0);

            return obj;
        }
        public static IshtarObject* ToIshtarObject(short dotnet_value, CallFrame frame = null)
        {
            var obj = IshtarGC.AllocObject(TYPE_I2.AsRuntimeClass());
            var clazz = IshtarUnsafe.AsRef<RuntimeIshtarClass>(obj->clazz);
            obj->vtable[clazz.Field["!!value"].vtable_offset] = (short*)dotnet_value;

            return obj;
        }
        public static IshtarObject* ToIshtarObject(byte dotnet_value, CallFrame frame = null)
        {
            var obj = IshtarGC.AllocObject(TYPE_I1.AsRuntimeClass());
            var clazz = IshtarUnsafe.AsRef<RuntimeIshtarClass>(obj->clazz);
            obj->vtable[clazz.Field["!!value"].vtable_offset] = (int*)dotnet_value;

            return obj;
        }
        public static IshtarObject* ToIshtarObject(long dotnet_value, CallFrame frame = null)
        {
            var obj = IshtarGC.AllocObject(TYPE_I8.AsRuntimeClass());
            var clazz = IshtarUnsafe.AsRef<RuntimeIshtarClass>(obj->clazz);
            obj->vtable[clazz.Field["!!value"].vtable_offset] = (long*)dotnet_value;

            return obj;
        }
        public static sbyte ToDotnetInt8(IshtarObject* obj, CallFrame frame)
        {
            FFI.StaticTypeOf(frame, &obj, TYPE_I1);
            var clazz = obj->DecodeClass();
            return (sbyte)(sbyte*)obj->vtable[clazz.Field["!!value"].vtable_offset];
        }
        public static short ToDotnetInt16(IshtarObject* obj, CallFrame frame)
        {
            FFI.StaticTypeOf(frame, &obj, TYPE_I2);
            var clazz = obj->DecodeClass();
            return (short)(short*)obj->vtable[clazz.Field["!!value"].vtable_offset];
        }
        public static int ToDotnetInt32(IshtarObject* obj, CallFrame frame)
        {
            FFI.StaticTypeOf(frame, &obj, TYPE_I4);
            var clazz = obj->DecodeClass();
            return (int)(int*)obj->vtable[clazz.Field["!!value"].vtable_offset];
        }
        public static long ToDotnetInt64(IshtarObject* obj, CallFrame frame)
        {
            FFI.StaticTypeOf(frame, &obj, TYPE_I8);
            var clazz = obj->DecodeClass();
            return (long)(long*)obj->vtable[clazz.Field["!!value"].vtable_offset];
        }

        public static byte ToDotnetUInt8(IshtarObject* obj, CallFrame frame)
        {
            FFI.StaticTypeOf(frame, &obj, TYPE_U1);
            var clazz = obj->DecodeClass();
            return (byte)(byte*)obj->vtable[clazz.Field["!!value"].vtable_offset];
        }
        public static ushort ToDotnetUInt16(IshtarObject* obj, CallFrame frame)
        {
            FFI.StaticTypeOf(frame, &obj, TYPE_U2);
            var clazz = obj->DecodeClass();
            return (ushort)(ushort*)obj->vtable[clazz.Field["!!value"].vtable_offset];
        }
        public static uint ToDotnetUInt32(IshtarObject* obj, CallFrame frame)
        {
            FFI.StaticTypeOf(frame, &obj, TYPE_U4);
            var clazz = obj->DecodeClass();
            return (uint)(uint*)obj->vtable[clazz.Field["!!value"].vtable_offset];
        }
        public static ulong ToDotnetUInt64(IshtarObject* obj, CallFrame frame)
        {
            FFI.StaticTypeOf(frame, &obj, TYPE_U8);
            var clazz = obj->DecodeClass();
            return (ulong)(ulong*)obj->vtable[clazz.Field["!!value"].vtable_offset];
        }
        public static bool ToDotnetBoolean(IshtarObject* obj, CallFrame frame)
        {
            FFI.StaticTypeOf(frame, &obj, TYPE_BOOLEAN);
            var clazz = obj->DecodeClass();
            return (int)(int*)obj->vtable[clazz.Field["!!value"].vtable_offset] == 1;
        }
        public static char ToDotnetChar(IshtarObject* obj, CallFrame frame)
        {
            FFI.StaticTypeOf(frame, &obj, TYPE_CHAR);
            var clazz = obj->DecodeClass();
            return (char)(int)(int*)obj->vtable[clazz.Field["!!value"].vtable_offset];
        }

        public static string ToDotnetString(IshtarObject* obj, CallFrame frame)
        {
            FFI.StaticTypeOf(frame, &obj, TYPE_STRING);
            var clazz = obj->DecodeClass();
            var p = (StrRef*)obj->vtable[clazz.Field["!!value"].vtable_offset];
            return StringStorage.GetString(p);
        }

        public static stackval UnBoxing(CallFrame frame, IshtarObject* obj)
        {
            var @class = obj->DecodeClass();

            var val = new stackval { type = @class.TypeCode };
            if (@class.TypeCode is TYPE_OBJECT or TYPE_CLASS or TYPE_STRING or TYPE_ARRAY)
            {
                val.data.p = (nint)obj;
                return val;
            }

            if (@class.TypeCode is TYPE_NONE or > TYPE_ARRAY or < TYPE_NONE)
            {
                VM.FastFail(WNE.ACCESS_VIOLATION,
                    "Scalar value type cannot be extracted.\n" +
                    "Invalid memory address is possible.\n" +
                    "Please report the problem into https://github.com/0xF6/mana_lang/issues.",
                    frame);
                VM.ValidateLastError();
                return default;
            }

            switch (val.type)
            {
                case TYPE_I1:
                    val.data.b = ToDotnetInt8(obj, frame);
                    break;
                case TYPE_I2:
                    val.data.s = ToDotnetInt16(obj, frame);
                    break;
                case TYPE_I4:
                    val.data.i = ToDotnetInt32(obj, frame);
                    break;
                case TYPE_I8:
                    val.data.l = ToDotnetInt64(obj, frame);
                    break;
                case TYPE_U1:
                    val.data.ub = ToDotnetUInt8(obj, frame);
                    break;
                case TYPE_U2:
                    val.data.us = ToDotnetUInt16(obj, frame);
                    break;
                case TYPE_U4:
                    val.data.ui = ToDotnetUInt32(obj, frame);
                    break;
                case TYPE_U8:
                    val.data.ul = ToDotnetUInt64(obj, frame);
                    break;
                case TYPE_BOOLEAN:
                    val.data.i = ToDotnetBoolean(obj, frame) ? 1 : 0;
                    break;
                case TYPE_CHAR:
                    val.data.i = ToDotnetChar(obj, frame);
                    break;
                case TYPE_R8 or TYPE_R2 or TYPE_R16 or TYPE_R4:
                    VM.FastFail(WNE.ACCESS_VIOLATION,
                        $"Scalar value type '{val.type}' cannot be extracted.\n" +
                        "Currently is not support.\n" +
                        "Please report the problem into https://github.com/0xF6/mana_lang/issues.",
                        frame);
                    VM.ValidateLastError();
                    return default;
            }

            return val;
        }
        public static IshtarObject* Boxing(CallFrame frame, stackval* p, IshtarObject** node = null)
        {
            if (p->type is TYPE_OBJECT or TYPE_CLASS or TYPE_STRING or TYPE_ARRAY)
                return (IshtarObject*)p->data.p;
            if (p->type is TYPE_NONE or > TYPE_ARRAY or < TYPE_NONE)
            {
                VM.FastFail(WNE.ACCESS_VIOLATION,
                    "Scalar value type cannot be extracted.\n" +
                    "Invalid memory address is possible.\n" +
                    "Please report the problem into https://github.com/0xF6/mana_lang/issues.",
                    frame);
                VM.ValidateLastError();
            }

            var clazz = p->type.AsRuntimeClass();
            var obj = IshtarGC.AllocObject(clazz);

            FFI.StaticValidateField(frame, &obj, "!!value");

            obj->vtable[clazz.Field["!!value"].vtable_offset] = p->type switch
            {
                TYPE_I1 => (sbyte*)p->data.b,
                TYPE_U1 => (byte*)p->data.ub,
                TYPE_I2 => (short*)p->data.s,
                TYPE_U2 => (ushort*)p->data.us,
                TYPE_I4 => (int*)p->data.i,
                TYPE_U4 => (uint*)p->data.ui,
                TYPE_I8 => (long*)p->data.l,
                TYPE_U8 => (ulong*)p->data.ul,

                TYPE_BOOLEAN => (int*)p->data.i,
                TYPE_CHAR => (int*)p->data.i,
                TYPE_ARRAY => (void*)p->data.p,

                _ => &*p
            };

            return obj;
        }
    }
}
