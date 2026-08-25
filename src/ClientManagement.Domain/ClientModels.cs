namespace ClientManagement.Domain;

public sealed class Client
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string IdentityNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<Address> Addresses { get; set; } = [];
    public List<Contact> Contacts { get; set; } = [];
}

public sealed class Address
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public sealed class Contact
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class ClientPage
{
    public IReadOnlyList<Client> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}