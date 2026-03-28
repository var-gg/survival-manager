using System;
using SM.Persistence.Abstractions;
using SM.Persistence.Abstractions.Models;

namespace SM.Persistence.Postgres;

public sealed class PostgresSaveRepository : ISaveRepository
{
    private readonly string _connectionString;

    public PostgresSaveRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Postgres connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public SaveProfile LoadOrCreate(string profileId)
    {
        // MVP placeholder adapter: intentionally non-blocking until a local dev DB dependency is wired.
        throw new NotSupportedException("Postgres adapter is reserved for local dev wiring and is not required for MVP runtime. JSON fallback should be used when DB is unavailable.");
    }

    public void Save(SaveProfile profile)
    {
        // MVP placeholder adapter: intentionally non-blocking until a local dev DB dependency is wired.
        throw new NotSupportedException("Postgres adapter is reserved for local dev wiring and is not required for MVP runtime. JSON fallback should be used when DB is unavailable.");
    }
}
