namespace AgeLanServer.Server.Models.Playfab;

/// <summary>
/// Giao diện hàm Cloud Script có tên.
/// </summary>
public interface INamed
{
    string Name();
}

/// <summary>
/// Giao diện hàm Cloud Script PlayFab.
/// Chạy hàm với tham số tổng quát và trả về kết quả.
/// </summary>
public interface ICloudScriptFunction : INamed
{
    object? Run(IGame game, IUser user, object? parameters);
    object? NewParameters();
}

/// <summary>
/// Giao diện hàm Cloud Script đặc thù với kiểu mạnh.
/// </summary>
public interface ISpecificCloudScriptFunction<in P, out R> : INamed
{
    R? RunTyped(IGame game, IUser user, P parameters);
}

/// <summary>
/// Lớp cơ sở cho hàm Cloud Script.
/// Cung cấp cơ chế wrapper từ kiểu mạnh sang kiểu object.
/// </summary>
public class CloudScriptFunctionBase<P, R> : ICloudScriptFunction
    where P : class, new()
    where R : class
{
    private readonly ISpecificCloudScriptFunction<P, R> _impl;

    public CloudScriptFunctionBase(ISpecificCloudScriptFunction<P, R> impl)
    {
        _impl = impl;
    }

    public object? NewParameters() => new P();

    public object? Run(IGame game, IUser user, object? parameters)
    {
        return _impl.RunTyped(game, user, (P)parameters!);
    }

    public string Name() => _impl.Name();
}
