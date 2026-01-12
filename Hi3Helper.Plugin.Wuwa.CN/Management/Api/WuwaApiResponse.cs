using System.Text.Json.Serialization;

// ReSharper disable IdentifierTypo

namespace Hi3Helper.Plugin.Wuwa.CN.Management.Api;

[JsonSerializable(typeof(WuwaApiResponseMedia))]
[JsonSerializable(typeof(WuwaApiResponseNews))]
[JsonSerializable(typeof(WuwaApiResponseSocial))]
[JsonSerializable(typeof(WuwaApiResponseGameConfig))]
public partial class WuwaApiResponseContext : JsonSerializerContext;