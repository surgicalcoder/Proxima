namespace ThreeOneThree.Proxima.Core.Entities
{
    public class Server : MongoEntity
    {
        public Server(string machineName)
        {
            MachineName = machineName.ToLowerInvariant();
        }

        public string MachineName { get; set; }
    }
}