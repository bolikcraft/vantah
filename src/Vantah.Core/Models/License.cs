namespace Vantah.Core.Models;

public sealed record License(string Email, string Plan, int MaxDevices, string? RenewalDate);
