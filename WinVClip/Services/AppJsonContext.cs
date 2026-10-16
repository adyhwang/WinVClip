using System.Text.Json.Serialization;

namespace WinVClip.Services
{
    /// <summary>
    /// System.Text.Json source generator 上下文：
    /// 编译期生成序列化代码，消除启动时高频 JSON 解析的反射开销
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(Models.AppSettings))]
    [JsonSerializable(typeof(LanguageData))]
    [JsonSerializable(typeof(Models.CharDataRoot))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}
