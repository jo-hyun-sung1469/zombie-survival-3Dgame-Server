using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace zombie_survival_3Dgame_Server.Common;

internal static class PersistenceErrors
{
    public static bool IsDuplicateKey(Exception exception)
    {
        if (exception is not DbUpdateException)
        {
            return false;
        }

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is MySqlException { Number: 1062 })
            {
                return true;
            }
        }

        return false;
    }
}
