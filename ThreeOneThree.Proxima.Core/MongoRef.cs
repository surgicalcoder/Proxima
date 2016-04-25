using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using ThreeOneThree.Proxima.Core.Entities;

namespace ThreeOneThree.Proxima.Core
{
    public class MongoRef<T> : IEquatable<MongoRef<T>> where T : MongoEntity
    {
        public MongoRef(string refId)
        {
            _refId = refId;
        }

        static public implicit operator MongoRef<T>(T item)
        {
            return new MongoRef<T>() { Reference = item };
        }

        static public implicit operator MongoRef<T>(string item)
        {
            return new MongoRef<T>(item);
        }
        
        private string _refId;

        public T Reference { get; set; }

        public string ReferenceId
        {
            get
            {
                if (Reference != null)
                {
                    _refId = Reference.Id;
                    return Reference.Id;
                }

                return _refId;
            }
            set { _refId = value; }
        }

        public override string ToString()
        {
            return ReferenceId;
        }

        public void Fetch(IList<T> items)
        {
            Reference = items.FirstOrDefault(f => f.Id == _refId);
        }

        static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }

        public bool Equals(MongoRef<T> other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return EqualityComparer<T>.Default.Equals(Reference, other.Reference);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != this.GetType())
            {
                return false;
            }
            return Equals((MongoRef<T>) obj);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<T>.Default.GetHashCode(Reference);
        }

        public static bool operator ==(MongoRef<T> left, MongoRef<T> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(MongoRef<T> left, MongoRef<T> right)
        {
            return !Equals(left, right);
        }

        public MongoRef()
        {
        }

        public string Type => typeof(T).FullName;
    }

}
