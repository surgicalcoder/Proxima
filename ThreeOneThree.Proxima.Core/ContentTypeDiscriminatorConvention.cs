using System;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Conventions;
using NLog;

namespace ThreeOneThree.Proxima.Core
{
    public class ContentTypeDiscriminatorConvention : IDiscriminatorConvention
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        public string ElementName
        {
            get { return "_t"; }
        }

        public Type GetActualType(IBsonReader bsonReader, Type nominalType)
        {
            var bookmark = bsonReader.GetBookmark();
            bsonReader.ReadStartDocument();
            string typeValue = string.Empty;
            if (bsonReader.FindElement(ElementName))
                typeValue = bsonReader.ReadString();
            else
                throw new NotSupportedException();

            bsonReader.ReturnToBookmark(bookmark);
            var retr = Type.GetType(typeValue) ?? Type.GetType("ThreeOneThree.Proxima.Core.Entities." + typeValue);
            return retr;
        }

        public MongoDB.Bson.BsonValue GetDiscriminator(Type nominalType, Type actualType)
        {
            return actualType.Name;
        }
    }
}