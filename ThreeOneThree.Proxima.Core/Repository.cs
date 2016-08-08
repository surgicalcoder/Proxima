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
using NLog;
using ThreeOneThree.Proxima.Core.Entities;
using Utilities.DataTypes;

namespace ThreeOneThree.Proxima.Core
{
    public class Repository : IDisposable
    {
        private string mongoContext;

        #region Props
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static DateTime InitLastChecked { get; set; }
        private static bool InitRun { get; set; }

        private IMongoDatabase mongoDatabase { get; set; }
        private MongoClient client { get; set; }
        public string ConnectionString { get; set; }

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
            mongoContext = "ProximaContext";
            InitContext();
        }

        public Repository(string connectionString)
        {
            ConnectionString = connectionString;
            mongoContext = "ProximaContext";
            InitContext();
        }

        private void InitContext()
        {
            RegisterConventions();

            MongoUrl mongoUrl;

            if (String.IsNullOrWhiteSpace(ConnectionString))
            {
                
                mongoUrl = new MongoUrl(ConfigurationManager.ConnectionStrings[mongoContext].ConnectionString);
            }
            else
            {
                mongoUrl = new MongoUrl(ConnectionString);
            }
            
            client = new MongoClient(mongoUrl.Url);
            mongoDatabase = client.GetDatabase(mongoUrl.DatabaseName);
            //logger.Trace("Connected to " + mongoUrl.ToString());
            InitDatabase();
        }

        private void RegisterConventions()
        {
            var pack = new ConventionPack { new IgnoreIfNullConvention(true), /*new MongoRefConvention(),*/ new IgnoreExtraElementsConvention(true)};

            ConventionRegistry.Register("Custom Conventions", pack, t => true);


            try
            {
                BsonSerializer.RegisterGenericSerializerDefinition(typeof (MongoRef<>), typeof (MongoRefSerializer<>));
            }
            catch (BsonSerializationException bsex) when (bsex.Message == "There is already a serializer mapping registered for type MongoRef<T>.")
            {
                
            }

            try
            {
             BsonSerializer.RegisterDiscriminatorConvention(typeof(FileAction), new ContentTypeDiscriminatorConvention());   
            }
            catch (Exception e) { }

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
            
            if (!mongoDatabase.GetCollection<RawUSNEntry>(GetCollectionNameForType<RawUSNEntry>("")).Indexes.List().Any())
            {
                mongoDatabase.GetCollection<RawUSNEntry>(GetCollectionNameForType<RawUSNEntry>("")).Indexes.CreateOne(Builders<RawUSNEntry>.IndexKeys.Ascending(e => e.Mountpoint).Ascending(e=>e.FRN).Ascending(e => e.USN));
            }

            if (!mongoDatabase.GetCollection<RawUSNEntry>(GetCollectionNameForType<FileAction>("")).Indexes.List().Any())
            {
                mongoDatabase.GetCollection<FileAction>(GetCollectionNameForType<FileAction>("")).Indexes.CreateOne(Builders<FileAction>.IndexKeys.Descending(e => e.USN).Descending(e => e.Mountpoint).Text(e=>e.RelativePath));
            }

            if (!mongoDatabase.GetCollection<RawUSNEntry>(GetCollectionNameForType<USNJournalSyncLog>("")).Indexes.List().Any())
            {
                mongoDatabase.GetCollection<USNJournalSyncLog>(GetCollectionNameForType<FileAction>("")).Indexes.CreateOne(Builders<USNJournalSyncLog>.IndexKeys.Ascending(e => e.Action.RelativePath).Descending(e => e.ActionStartDate).Descending(e => e.ActionFinishDate));
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

            return mongoCollection.Find(predicate).ToListAsync().Result.FirstOrDefault();
        }

        public long Count<T>(Expression<Func<T, bool>> predicate, string OverrideCollectionName = "") where T : MongoEntity
        {
            var mongoCollection = mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName));
            
            return mongoCollection.Count(predicate);

        }

        public IQueryable<T> Many<T>(Expression<Func<T, bool>> predicate, int limit=0, Expression<Func<T, object>> AscendingSort=null, Expression<Func<T, object>> DescendingSort = null, string OverrideCollectionName = "") where T : MongoEntity
        {
            var mongoCollection = mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName));

            var retr = mongoCollection.Find(predicate);
            
            if (limit > 0)
            {
                retr = retr.Limit(limit);
            }

            if (AscendingSort != null)
            {
                retr = retr.SortBy(AscendingSort);
            }

            if (DescendingSort != null)
            {
                retr = retr.SortByDescending(DescendingSort);
            }

            return retr.ToListAsync().Result.AsQueryable();
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
            if (entities.Any())
            {
                mongoDatabase.GetCollection<T>(GetCollectionNameForType<T>(OverrideCollectionName)).InsertManyAsync(entities).Wait();
            }
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