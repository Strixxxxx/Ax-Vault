using System;
using System.Collections.Generic;

namespace Backend.ScaffoldModels;

public partial class Account
{
    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string UniqueKey { get; set; } = null!;

    public string DatabaseName { get; set; } = null!;
}
