using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace ThreeOneThree.Proxima.Core
{
    public class MongoRefConvention : ConventionBase, IClassMapConvention
    {
        public void Apply(BsonClassMap classMap)
        {
            if (classMap.ClassType.IsGenericType && classMap.ClassType.GetGenericTypeDefinition() == typeof(MongoRef<>))
            {
                classMap.UnmapProperty("Reference");
            }

        }
    }
}