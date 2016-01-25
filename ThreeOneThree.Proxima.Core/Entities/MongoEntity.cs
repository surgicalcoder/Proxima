using MongoDB.Bson;

namespace ThreeOneThree.Proxima.Core.Entities
{
    public abstract class MongoEntity
    {
        public string Id { get; set; }
    }
}