using System;
using MongoDB.Bson;
using MongoDB.Bson.IO;
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

            context.Writer.WriteObjectId(new ObjectId(value.ReferenceId));
        }

        public override MongoRef<T> Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (context.Reader.State == BsonReaderState.Name)
            {
                context.Reader.ReadStartDocument();

                return new MongoRef<T>(context.Reader.ReadObjectId().ToString());
            }

            if (context.Reader.CurrentBsonType == BsonType.Document)
            {
                context.Reader.ReadStartDocument();
                return new MongoRef<T>(context.Reader.ReadObjectId().ToString());
            }
            else
            {
                return new MongoRef<T>(context.Reader.ReadObjectId().ToString());
            }
        }
    }
}