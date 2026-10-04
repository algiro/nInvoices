using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace nInvoices.Infrastructure.Data;

/// <summary>
/// The model's encryption converters hold the context's <see cref="Encryption.FieldEncryptor"/>, so a
/// context with another encryptor (in tests) needs its own model instead of the cached one.
/// </summary>
internal sealed class EncryptorModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        context is ApplicationDbContext app
            ? (context.GetType(), app.Encryptor, designTime)
            : (context.GetType(), designTime);
}
