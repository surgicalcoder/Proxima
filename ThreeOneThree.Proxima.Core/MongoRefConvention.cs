using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Core
{
    //public class MongoRefConvention : ConventionBase, IClassMapConvention
    //{
    //    public void Apply(BsonClassMap classMap)
    //    {
    //        if (classMap.ClassType.IsGenericType && classMap.ClassType.GetGenericTypeDefinition() == typeof(MongoRef<>))
    //        {
    //            classMap.UnmapProperty("Reference");
    //        }

    //    }
    //}

    public class MongoRefSerializer<T> : SerializerBase<MongoRef<T>> where T : MongoEntity
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, MongoRef<T> value)
        {
            context.Writer.WriteString(value.ReferenceId);
        }

        public override MongoRef<T> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return new MongoRef<T>(context.Reader.ReadString());
        }
    }
}