using Microsoft.Data.Sqlite;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Repositories
{
    public class JobsRepository
    {
        private readonly string _dbPath;
        public JobsRepository(string dbPath)
        {
            _dbPath = dbPath;
        }

        private SqliteConnection Open()
        {
            var dir = Path.GetDirectoryName(_dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var cs = new SqliteConnectionStringBuilder { DataSource = _dbPath };
            var conn = new SqliteConnection(cs.ToString());
            conn.Open();
            return conn;
        }

        public void EnsureCreated()
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS jobs (
                id TEXT PRIMARY KEY,
                x REAL,
                y REAL,
                weightKg REAL,
                priority INTEGER,
                earliestTime REAL,
                latestTime REAL,
                zone TEXT,
                quantity INTEGER,
                orderId TEXT,
                rackId TEXT,
                corridor TEXT,
                createdAt TEXT
            );";
            cmd.ExecuteNonQuery();
        }

        public void InsertJobs(IEnumerable<JobTask> jobs)
        {
            using var conn = Open();
            using var tx = conn.BeginTransaction();
            foreach (var j in jobs)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                INSERT OR REPLACE INTO jobs (id, x, y, weightKg, priority, earliestTime, latestTime, zone, quantity, orderId, rackId, corridor, createdAt)
                VALUES ($id, $x, $y, $weightKg, $priority, $earliestTime, $latestTime, $zone, $quantity, $orderId, $rackId, $corridor, $createdAt);
                ";
                cmd.Parameters.AddWithValue("$id", j.Id);
                cmd.Parameters.AddWithValue("$x", j.Location?.X ?? 0);
                cmd.Parameters.AddWithValue("$y", j.Location?.Y ?? 0);
                cmd.Parameters.AddWithValue("$weightKg", j.WeightKg ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$priority", j.Priority);
                cmd.Parameters.AddWithValue("$earliestTime", j.EarliestTime ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$latestTime", j.LatestTime ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$zone", j.Zone ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$quantity", j.Quantity ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$orderId", j.OrderId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$rackId", j.RackId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$corridor", j.Corridor ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        public List<JobTask> GetAllJobs()
        {
            var list = new List<JobTask>();
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, x, y, weightKg, priority, earliestTime, latestTime, zone, quantity, orderId, rackId, corridor, createdAt FROM jobs ORDER BY createdAt DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var job = new JobTask
                {
                    Id = reader.GetString(0),
                    Location = new LocationPoint { X = reader.IsDBNull(1) ? 0 : reader.GetDouble(1), Y = reader.IsDBNull(2) ? 0 : reader.GetDouble(2) },
                    WeightKg = reader.IsDBNull(3) ? null : reader.GetDouble(3),
                    Priority = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    EarliestTime = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                    LatestTime = reader.IsDBNull(6) ? null : reader.GetDouble(6),
                    Zone = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Quantity = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    OrderId = reader.IsDBNull(9) ? null : reader.GetString(9),
                    RackId = reader.IsDBNull(10) ? null : reader.GetString(10),
                    Corridor = reader.IsDBNull(11) ? null : reader.GetString(11),
                };
                list.Add(job);
            }
            return list;
        }
    }
}