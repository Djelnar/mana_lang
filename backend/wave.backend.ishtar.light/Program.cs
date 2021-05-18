﻿namespace wave.backend.ishtar.light
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using fs;
    using global::ishtar;
    using runtime;
    using wave.ishtar.emit;


    internal class Program
    {
        public static List<WaveModule> GetDeps()
        {
            var list = new List<WaveModule>();


            var stl = new WaveModuleBuilder("wcorlib", new Version(1,0));

            foreach (var type in WaveCore.Types.All) 
                stl.InternTypeName(type.FullName);
            foreach (var type in WaveCore.All)
            {
                stl.InternTypeName(type.FullName);
                stl.InternString(type.Name);
                stl.InternString(type.Path);
                foreach (var field in type.Fields)
                {
                    stl.InternFieldName(field.FullName);
                    stl.InternString(field.Name);
                }
                foreach (var method in type.Methods)
                {
                    stl.InternString(method.Name);
                    foreach (var argument in method.Arguments)
                    {
                        stl.InternString(argument.Name);
                    }
                }
                stl.class_table.Add(type);
            }
            list.Add(stl);
            return list;
        }
        public static unsafe int Main(string[] args)
        {
            //while (!Debugger.IsAttached)
            //    Thread.Sleep(200);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Console.OutputEncoding = Encoding.Unicode;
            IshtarCore.INIT();
            foreach (var @class in WaveCore.All.OfType<RuntimeIshtarClass>()) 
                @class.init_vtable();
            IshtarGC.INIT();
            FFI.INIT();

            var masterModule = default(IshtarAssembly);
            var resolver = new AssemblyResolver();

            if (AssemblyBundle.IsBundle(out var bundle))
            {
                masterModule = bundle.Assemblies.First();
                resolver.AddInMemory(bundle);
            }
            else
            {
                if (args.Length < 1)
                    return -1;
                var entry = new FileInfo(args.First());
                if (!entry.Exists)
                    return -2;
                masterModule = IshtarAssembly.LoadFromFile(entry);
            }
            
            var (_, code) = masterModule.Sections.First();
            var deps = GetDeps();
            
            resolver.AddSearchPath(new DirectoryInfo("/WaveLang"));
            resolver.AddSearchPath(new DirectoryInfo("./"));

            var module = RuntimeModuleReader.Read(code, deps, (s, version) => 
                resolver.ResolveDep(s, version, deps));

            foreach (var @class in module.class_table.OfType<RuntimeIshtarClass>())
            {
                @class.init_vtable();
                VM.ValidateLastError();
            }

            module.Deps.Add(deps.First());

            var entry_point = module.GetEntryPoint();

            if (entry_point is null)
            {
                VM.FastFail(WNE.MISSING_METHOD, "Entry point is not defined.");
                VM.ValidateLastError();
                return -280;
            }

            var args_ = stackalloc stackval[1];

            var frame = new CallFrame
            {
                args = args_, 
                method = entry_point, 
                level = 0
            };
            

            var watcher = Stopwatch.StartNew();
            VM.exec_method(frame);

            if (frame.exception is not null)
                Console.WriteLine($"unhandled exception was thrown. \n" +
                                  $"{frame.exception.stack_trace}");

            watcher.Stop();
            Console.WriteLine($"Elapsed: {watcher.Elapsed}");
            
            return 0;
        }


        
    }


    public class AssemblyBundle
    {
        public FileInfo MainModulePath { get; set; }
        public List<byte> MainModuleBytes { get; set; }

        public List<IshtarAssembly> Assemblies { get; private set; }


        public static bool IsBundle(out AssemblyBundle bundle)
        {
            var current = Process.GetCurrentProcess()?.MainModule?.FileName;
            bundle = null;
            if (string.IsNullOrEmpty(current))
            {
                VM.FastFail(WNE.STATE_CORRUPT, "Current executable has corrupted. [process file not found]");
                VM.ValidateLastError();
                return false;
            }

            using var stream = File.OpenRead(current);
            stream.Seek(-sizeof(short), SeekOrigin.End);
            var magicBytes = stream.ReadBytes(sizeof(short));

            if (BitConverter.ToInt16(magicBytes, 0) != 0x7ABC)
                return false;
            stream.Seek(-(sizeof(short) + sizeof(int)), SeekOrigin.End);
            var offset = BitConverter.ToInt32(stream.ReadBytes(sizeof(int)));
            stream.Seek(offset, SeekOrigin.Begin);
            var bytes = stream.ReadToEnd().SkipLast(sizeof(short) + sizeof(int)).ToList();

            bundle = new AssemblyBundle
            {
                MainModuleBytes = bytes, 
                MainModulePath = new FileInfo(current)
            }.UnpackAssemblies();
            
            return true;
        }


        private AssemblyBundle UnpackAssemblies()
        {
            Assemblies = new List<IshtarAssembly>();


            var offset_bytes = MainModuleBytes.SkipLast(sizeof(short)).TakeLast(sizeof(int)).ToArray();
            var offset = BitConverter.ToInt32(offset_bytes);

            var input = MainModuleBytes.SkipLast(sizeof(short) + sizeof(int)).Skip(offset).ToArray();
            using var mem = new MemoryStream(input); // todo multiple modules
            Assemblies.Add(IshtarAssembly.LoadFromMemory(mem));

            return this;
        }
    }

    internal static class FileStreamEx
    {
        public static byte[] ReadBytes(this FileStream stream, int count)
        {
            Span<byte> bytes = stackalloc byte[count];
            for (var i = 0; i != count; i++)
            {
                var b = stream.ReadByte();
                if (b == -1)
                    return new byte[0];
                bytes[i] = (byte) b;
            }
            return bytes.ToArray();
        }

        public static byte[] ReadToEnd(this FileStream stream)
        {
            var count = stream.Length - stream.Position;
            Span<byte> bytes = stackalloc byte[(int)count];
            stream.Read(bytes);
            return bytes.ToArray();
        }
    }
}
