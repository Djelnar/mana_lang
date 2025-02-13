namespace mana.project
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Newtonsoft.Json;

    public class ManaSDK
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("packs")]
        [JsonConverter(typeof(PacksConverter))]
        public SDKPack[] Packs { get; set; }

        internal static DirectoryInfo SDKRoot =>
            new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".mana"));

        public string GetFullPath(SDKPack sdkPack) =>
            SDKRoot.SubDirectory("sdk")
                .SubDirectory($"{Name}-v{Version}")
                .ThrowIfNotExist($"'{Name}-v{Version}' is not installed.")
                .SubDirectory(sdkPack.Name)
                .FullName;

        public SDKPack GetDefaultPack()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetPackByAlias("win10-x64");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetPackByAlias("osx-x64");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return GetPackByAlias("linux-x64");
            throw new NotSupportedException("OS is not support");
        }

        public SDKPack GetPackByAlias(string alias) =>
            Packs.FirstOrDefault(x => x.Alias.Equals(alias)) ??
            throw new DirectoryNotFoundException($"Pack '{alias}' not installed in '{Name}' sdk.");

        public static ManaSDK? Resolve(string name)
        {
            if (!SDKRoot.Exists)
                throw new SDKNotInstalled($"Sdk is not installed.");

            return SDKRoot
                .SubDirectory("manifest")
                .EnumerateFiles("*.manifest.json")
                .Select(json => json.FullName)
                .Select(File.ReadAllText)
                .Select(JsonConvert.DeserializeObject<ManaSDK>)
                .FirstOrDefault(x => x.Name.Equals(name));
        }

        public FileInfo GetHostApplicationFile(SDKPack sdkPack) =>
            SDKRoot.SubDirectory("sdk")
                .SubDirectory($"{Name}-v{Version}")
                .SubDirectory(sdkPack.Name)
                .SubDirectory("host")
                .SingleFileByPattern("host.*");
    }
    public enum PackKind
    {
        Sdk,
        Template,
        Tools,
        Resources
    }

    public class SDKNotInstalled : Exception
    {
        public SDKNotInstalled(string msg) : base(msg)
        {

        }
    }
    public class SDKPack
    {
        public string Name { get; set; }
        [JsonProperty("kind")]
        public PackKind Kind { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("alias")]
        public string Alias { get; set; }
    }
}
