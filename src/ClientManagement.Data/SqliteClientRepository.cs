using System.Globalization;
using System.Text;
using ClientManagement.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace ClientManagement.Data;

public sealed class SqliteClientRepository : IClientRepository
{
    private readonly string _connectionString;

    public SqliteClientRepository(IConfiguration configuration)
    {
        var path = configuration["DatabasePath"] ?? "client-management.db";
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS Clients (Id INTEGER PRIMARY KEY AUTOINCREMENT, FirstName TEXT NOT NULL, LastName TEXT NOT NULL, Gender TEXT NOT NULL, DateOfBirth TEXT NULL, IdentityNumber TEXT NOT NULL DEFAULT '', Notes TEXT NOT NULL DEFAULT '');
            CREATE TABLE IF NOT EXISTS Addresses (Id INTEGER PRIMARY KEY AUTOINCREMENT, ClientId INTEGER NOT NULL, Type TEXT NOT NULL, Line1 TEXT NOT NULL, Line2 TEXT NOT NULL DEFAULT '', City TEXT NOT NULL DEFAULT '', Region TEXT NOT NULL DEFAULT '', PostalCode TEXT NOT NULL DEFAULT '', Country TEXT NOT NULL DEFAULT '', FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS Contacts (Id INTEGER PRIMARY KEY AUTOINCREMENT, ClientId INTEGER NOT NULL, Type TEXT NOT NULL, Value TEXT NOT NULL, FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE CASCADE);
            CREATE INDEX IF NOT EXISTS IX_Clients_Name ON Clients(LastName, FirstName);
            CREATE INDEX IF NOT EXISTS IX_Addresses_Client ON Addresses(ClientId);
            CREATE INDEX IF NOT EXISTS IX_Contacts_Client ON Contacts(ClientId);
            """;
        command.ExecuteNonQuery();
    }

    public ClientPage Search(string? search, int page, int pageSize)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        using var connection = Open();
        var term = search?.Trim() ?? string.Empty;
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Clients WHERE @search = '' OR FirstName LIKE @like OR LastName LIKE @like OR IdentityNumber LIKE @like";
        count.Parameters.AddWithValue("@search", term); count.Parameters.AddWithValue("@like", $"%{term}%");
        var total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, FirstName, LastName, Gender, DateOfBirth, IdentityNumber, Notes FROM Clients WHERE @search = '' OR FirstName LIKE @like OR LastName LIKE @like OR IdentityNumber LIKE @like ORDER BY LastName, FirstName LIMIT @size OFFSET @offset";
        command.Parameters.AddWithValue("@search", term); command.Parameters.AddWithValue("@like", $"%{term}%"); command.Parameters.AddWithValue("@size", pageSize); command.Parameters.AddWithValue("@offset", (page - 1) * pageSize);
        var items = new List<Client>(); using var reader = command.ExecuteReader();
        while (reader.Read()) items.Add(ReadClient(reader));
        return new ClientPage { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public Client? Get(int id)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, FirstName, LastName, Gender, DateOfBirth, IdentityNumber, Notes FROM Clients WHERE Id = @id"; command.Parameters.AddWithValue("@id", id);
        using var reader = command.ExecuteReader(); if (!reader.Read()) return null; var client = ReadClient(reader); reader.Close(); LoadChildren(connection, client); return client;
    }

    public int Save(Client client)
    {
        using var connection = Open(); using var transaction = connection.BeginTransaction(); using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = client.Id == 0 ? "INSERT INTO Clients (FirstName, LastName, Gender, DateOfBirth, IdentityNumber, Notes) VALUES (@first, @last, @gender, @dob, @identity, @notes); SELECT last_insert_rowid();" : "UPDATE Clients SET FirstName=@first, LastName=@last, Gender=@gender, DateOfBirth=@dob, IdentityNumber=@identity, Notes=@notes WHERE Id=@id; SELECT @id;";
        command.Parameters.AddWithValue("@first", client.FirstName.Trim()); command.Parameters.AddWithValue("@last", client.LastName.Trim()); command.Parameters.AddWithValue("@gender", client.Gender.Trim()); command.Parameters.AddWithValue("@dob", client.DateOfBirth?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value); command.Parameters.AddWithValue("@identity", client.IdentityNumber.Trim()); command.Parameters.AddWithValue("@notes", client.Notes.Trim()); if (client.Id != 0) command.Parameters.AddWithValue("@id", client.Id);
        client.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        using var clear = connection.CreateCommand(); clear.Transaction = transaction; clear.CommandText = "DELETE FROM Addresses WHERE ClientId=@id; DELETE FROM Contacts WHERE ClientId=@id;"; clear.Parameters.AddWithValue("@id", client.Id); clear.ExecuteNonQuery();
        foreach (var address in client.Addresses.Where(a => !string.IsNullOrWhiteSpace(a.Type) && !string.IsNullOrWhiteSpace(a.Line1))) AddAddress(connection, transaction, client.Id, address);
        foreach (var contact in client.Contacts.Where(c => !string.IsNullOrWhiteSpace(c.Type) && !string.IsNullOrWhiteSpace(c.Value))) AddContact(connection, transaction, client.Id, contact);
        transaction.Commit(); return client.Id;
    }

    public void Delete(int id) { using var c = Open(); using var x = c.CreateCommand(); x.CommandText = "DELETE FROM Clients WHERE Id=@id"; x.Parameters.AddWithValue("@id", id); x.ExecuteNonQuery(); }

    public byte[] ExportCsv()
    {
        using var connection = Open(); using var command = connection.CreateCommand(); command.CommandText = "SELECT c.FirstName, c.LastName, c.Gender, c.DateOfBirth, c.IdentityNumber, c.Notes, a.Type, a.Line1, a.Line2, a.City, a.Region, a.PostalCode, a.Country FROM Clients c LEFT JOIN Addresses a ON a.ClientId=c.Id ORDER BY c.LastName, c.FirstName";
        using var reader = command.ExecuteReader(); var csv = new StringBuilder("FirstName,LastName,Gender,DateOfBirth,IdentityNumber,Notes,AddressType,Line1,Line2,City,Region,PostalCode,Country\r\n");
        while (reader.Read()) csv.AppendLine(string.Join(',', Enumerable.Range(0, 13).Select(i => Csv(reader.IsDBNull(i) ? string.Empty : reader.GetValue(i).ToString() ?? string.Empty))));
        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private SqliteConnection Open() { var connection = new SqliteConnection(_connectionString); connection.Open(); return connection; }
    private static Client ReadClient(SqliteDataReader r) => new() { Id = r.GetInt32(0), FirstName = r.GetString(1), LastName = r.GetString(2), Gender = r.GetString(3), DateOfBirth = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4), CultureInfo.InvariantCulture), IdentityNumber = r.GetString(5), Notes = r.GetString(6) };
    private static void LoadChildren(SqliteConnection c, Client client) { using var a = c.CreateCommand(); a.CommandText = "SELECT Id, Type, Line1, Line2, City, Region, PostalCode, Country FROM Addresses WHERE ClientId=@id"; a.Parameters.AddWithValue("@id", client.Id); using var r = a.ExecuteReader(); while (r.Read()) client.Addresses.Add(new Address { Id = r.GetInt32(0), ClientId = client.Id, Type = r.GetString(1), Line1 = r.GetString(2), Line2 = r.GetString(3), City = r.GetString(4), Region = r.GetString(5), PostalCode = r.GetString(6), Country = r.GetString(7) }); r.Close(); using var x = c.CreateCommand(); x.CommandText = "SELECT Id, Type, Value FROM Contacts WHERE ClientId=@id"; x.Parameters.AddWithValue("@id", client.Id); using var z = x.ExecuteReader(); while (z.Read()) client.Contacts.Add(new Contact { Id = z.GetInt32(0), ClientId = client.Id, Type = z.GetString(1), Value = z.GetString(2) }); }
    private static void AddAddress(SqliteConnection c, SqliteTransaction t, int id, Address a) { using var x = c.CreateCommand(); x.Transaction = t; x.CommandText = "INSERT INTO Addresses (ClientId,Type,Line1,Line2,City,Region,PostalCode,Country) VALUES (@id,@type,@line1,@line2,@city,@region,@postal,@country)"; Add(x, id, a.Type, a.Line1, a.Line2, a.City, a.Region, a.PostalCode, a.Country); x.ExecuteNonQuery(); }
    private static void AddContact(SqliteConnection c, SqliteTransaction t, int id, Contact a) { using var x = c.CreateCommand(); x.Transaction = t; x.CommandText = "INSERT INTO Contacts (ClientId,Type,Value) VALUES (@id,@type,@value)"; x.Parameters.AddWithValue("@id", id); x.Parameters.AddWithValue("@type", a.Type.Trim()); x.Parameters.AddWithValue("@value", a.Value.Trim()); x.ExecuteNonQuery(); }
    private static void Add(SqliteCommand x, int id, string type, string line1, string line2, string city, string region, string postal, string country) { x.Parameters.AddWithValue("@id", id); x.Parameters.AddWithValue("@type", type.Trim()); x.Parameters.AddWithValue("@line1", line1.Trim()); x.Parameters.AddWithValue("@line2", line2.Trim()); x.Parameters.AddWithValue("@city", city.Trim()); x.Parameters.AddWithValue("@region", region.Trim()); x.Parameters.AddWithValue("@postal", postal.Trim()); x.Parameters.AddWithValue("@country", country.Trim()); }
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}