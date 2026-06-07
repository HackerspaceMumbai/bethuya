using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Aspire.Hosting.Azure;

namespace AppHost.Extensions;

public static class DatabaseExtensions
{
    public static IResourceBuilder<AzureSqlDatabaseResource> ConfigureDatabase(this IDistributedApplicationBuilder builder)
    {
        var sql = builder.AddAzureSqlServer("sql")
                                                            .RunAsContainer()
                                                            .AddDatabase("BethuyaDb");
        return sql;
    }

}
