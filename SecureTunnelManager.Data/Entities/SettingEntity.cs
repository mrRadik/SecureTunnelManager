namespace SecureTunnelManager.Data.Entities;

public class SettingEntity
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
