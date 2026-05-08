public class ClientModel
{
    public string Username { get; set; }
    public int ClientID { get; set; }
    public bool IsConnected { get; set; }

    public ClientModel()
    {
        Username = "";
        ClientID = -1;
        IsConnected = false;
    }
}