using G4.Cache;

using LiteDB;

namespace G4.Api.Abstractions
{
    public abstract class ClientBase
    {
        protected ClientBase()
        {
            LiteDatabase = CacheManager.LiteDatabase;
        }

        public ILiteDatabase LiteDatabase { get; }
    }
}
