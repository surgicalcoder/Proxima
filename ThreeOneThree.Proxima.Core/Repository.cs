using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Utilities.DataTypes;

namespace ThreeOneThree.Proxima.Core
{
    public class Repository : IDisposable
    {
        private string mongoContext;

        #region Props

        private static DateTime InitLastChecked { get; set; }
        private static bool InitRun { get; set; }

        private IMongoDatabase mongoDatabase { get; set; }
        private MongoClient client { get; set; }
        //private MongoServer mongoServer { get; set; }

        #endregion

        #region Creation

        public string MongoContext
        {
            get { return mongoContext; }
            set
            {
                mongoContext = value;
                InitDatabase();
            }
        }

        public Repository()
        {
            mongoContext = "MongoPluginContext";
            InitContext();
        }

        private void InitContext()
        {
            RegisterConventions();
            client = new MongoClient(ConfigurationManager.ConnectionStrings[mongoContext].ConnectionString);

            //mongoServer = client.GetServer();
            mongoDatabase = client.GetDatabase(ConfigurationManager.ConnectionStrings[mongoContext].ConnectionString.Substring(ConfigurationManager.ConnectionStrings[mongoContext].ConnectionString.LastIndexOf("/") + 1));

            InitDatabase();
        }

        //public Repository(string MongoContext)
        //{
        //    this.MongoContext = MongoContext;

        //    InitContext();
        //}

        private void RegisterConventions()
        {
            var pack = new ConventionPack { new IgnoreIfNullConvention(true), new MongoRefConvention(), new IgnoreExtraElementsConvention(true) };

            ConventionRegistry.Register("Custom Conventions", pack, t => true);


            if (!BsonClassMap.IsClassMapRegistered(typeof(MongoEntity)))
            {
                BsonClassMap.RegisterClassMap<MongoEntity>(delegate (BsonClassMap<MongoEntity> map)
                {
                    map.AutoMap();

                    //map.MapProperty(f => f.ParentId).SetSerializer(new StringSerializer().WithRepresentation(BsonType.ObjectId));

                    map.IdMemberMap.SetSerializer(new StringSerializer().WithRepresentation(BsonType.ObjectId)).SetIdGenerator(StringObjectIdGenerator.Instance); ;
                });
            }
        }

        public void InitDatabase()
        {
            PopulateDatabase();
        }

        private void PopulateDatabase()
        {
            if (InitRun && InitLastChecked > DateTime.Now.AddMinutes(-30))
            {
                return;
            }
            InitRun = true;
            InitLastChecked = DateTime.Now;
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool val)
        {
            if (val)
            {
                //TODO:Need disposal
            }
        }

        public void DisposeConnection()
        {
            Dispose();
        }

        #endregion

        private string GetCollectionNameForType<T>(string collectionNameOverride)
        {
            if (string.IsNullOrWhiteSpace(collectionNameOverride))
            {
                return typeof(T).Name.Replace(".", "");
            }
            else
            {

                return (collectionNameOverride + "-" + typeof(T).Name).Replace(".", "");
            }
        }

        #region Get

        public T ById<T>(string id, string OverrideCollectionName = "") where T : MongoEntity
        {
            return ById<T>(id, OverrideCollectionName, null);
        }
        public T ById<T>(string id, string OverrideCollectionName = "", params Expression<Func<T, dynamic>>[] PreLoad) where T : MongoEntity
        {
            var mongoCollection = mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName));


            //if (!mongoCollection.Exists())
            //{
            //    return null;
            //}


            var wibble = mongoCollection.Find(e => e.Id == id).ToListAsync().Result;


            var val = wibble.FirstOrDefault();

            if (PreLoad != null)
            {
                foreach (var item in PreLoad)
                {
                    var propName = item.GetPropertyName();

                    var theProp = typeof(T).GetProperty(propName);

                    if (theProp.GetType() != typeof(MongoRef<>) || String.IsNullOrWhiteSpace(item.Property("ReferenceId") as string))
                    {
                        continue;
                    }

                    var makeGenericMethod = GetType().GetMethod("ById").MakeGenericMethod(new[] { theProp.PropertyType.GenericTypeArguments[0] });

                    theProp.SetValue(val,
                        makeGenericMethod.Invoke(this,
                            new[] { item.Property("ReferenceId").ToString(), OverrideCollectionName, null }));

                }
            }

            return val;
        }

        public T One<T>(Expression<Func<T, bool>> predicate, string OverrideCollectionName = "") where T : MongoEntity
        {
            var mongoCollection = mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName));

            //if (!mongoCollection.Exists())
            //{
            //    return null;
            //}
            return mongoCollection.Find(predicate).ToListAsync().Result.FirstOrDefault();
        }

        public IQueryable<T> Many<T>(Expression<Func<T, bool>> predicate, string OverrideCollectionName = "") where T : MongoEntity
        {
            var mongoCollection = mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName));
            //if (!mongoCollection.Exists())
            //{
            //    return new List<T>().AsQueryable();
            //}
            //return mongoCollection.AsQueryable().Where(predicate);
            return mongoCollection.Find(predicate).ToListAsync().Result.AsQueryable();
        }

        public IQueryable<T> All<T>(string OverrideCollectionName = "") where T : MongoEntity
        {
            return mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName)).Find(e => true).ToListAsync().Result.AsQueryable();
        }

        #endregion

        #region Add

        public void Add<T>(T entity, string OverrideCollectionName = "") where T : MongoEntity
        {
            mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName)).InsertOneAsync(entity).Wait();//.Insert(entity);
        }

        public void Add<T>(IEnumerable<T> entities, string OverrideCollectionName = "") where T : MongoEntity
        {
            mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName)).InsertManyAsync(entities).Wait();
        }

        public void BulkInsert<T>(IEnumerable<T> entities, string OverrideCollectionName = "") where T : MongoEntity
        {
            
            mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName)).InsertManyAsync(entities).Wait();
        }

        #endregion

        #region Delete

        public void Delete<T>(T entity, string OverrideCollectionName = "") where T : MongoEntity
        {
            mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName)).DeleteOneAsync(f => f.Id == entity.Id).Wait();
        }

        #endregion

        #region Update

        public void Upsert<T>(T entity, string OverrideCollectionName = "") where T : MongoEntity
        {
            //var query = Query<T>.EQ(e => e.Id, entity.Id);

            // IMongoUpdate update = global::MongoDB.Driver.Builders.Update.Replace<T>(entity);


            var updateResult = mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName))
                                            .ReplaceOneAsync(e => e.Id == entity.Id, entity,
                                                new UpdateOptions() { IsUpsert = true })
                                            .Result;


            //WriteConcernResult writeConcernResult = mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName)).Update(query, update, UpdateFlags.Upsert);

            if (!updateResult.IsAcknowledged)
            {
                throw new Exception("Failing to upsert");
            }
        }

        public bool Update<T>(T entity, string OverrideCollectionName = "") where T : MongoEntity
        {
            //var query = Query<T>.EQ(e => e.Id, entity.Id);

            //IMongoUpdate update = global::MongoDB.Driver.Builders.Update.Replace<T>(entity);

            //WriteConcernResult writeConcernResult = mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName)).Update(query, update);

            //if (writeConcernResult.DocumentsAffected == 0)
            //{
            //    throw new Exception("Failing to update");
            //}

            var updateResult = mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName))
                                            .ReplaceOneAsync(e => e.Id == entity.Id, entity,
                                                new UpdateOptions() { IsUpsert = false })
                                            .Result;


            //WriteConcernResult writeConcernResult = mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName)).Update(query, update, UpdateFlags.Upsert);

            if (!updateResult.IsAcknowledged)
            {
                throw new Exception("Failing to update");
            }

            return true;
        }

        #endregion
    }
}