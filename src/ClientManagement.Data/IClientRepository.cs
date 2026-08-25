using ClientManagement.Domain;

namespace ClientManagement.Data;

public interface IClientRepository
{
    ClientPage Search(string? search, int page, int pageSize);
    Client? Get(int id);
    int Save(Client client);
    void Delete(int id);
    byte[] ExportCsv();
}