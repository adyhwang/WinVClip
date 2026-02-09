using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WinVClip.Services
{
    public class DatabaseService : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly string _dbPath;
        private bool _disposed;

        public string DatabasePath => _dbPath;

        public DatabaseService(string dbPath)
        {
            _dbPath = dbPath;
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? ".");
            _connection = new SqliteConnection($"Data Source={dbPath}");
            _connection.Open();
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ClipboardItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Type INTEGER NOT NULL,
                    Content TEXT,
                    ImagePath TEXT,
                    ImageHash TEXT,
                    FilePaths TEXT,
                    CreatedAt DATETIME NOT NULL,
                    PreviewText TEXT,
                    GroupId INTEGER DEFAULT NULL
                );
                CREATE TABLE IF NOT EXISTS Groups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL UNIQUE,
                    CreatedAt DATETIME NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_createdat ON ClipboardItems(CreatedAt DESC);
                CREATE INDEX IF NOT EXISTS idx_type ON ClipboardItems(Type);
                CREATE INDEX IF NOT EXISTS idx_imagehash ON ClipboardItems(ImageHash);
                CREATE INDEX IF NOT EXISTS idx_groupid ON ClipboardItems(GroupId);
            ";
            command.ExecuteNonQuery();

            EnsureGroupIdColumn();
            EnsureDefaultGroup();
        }

        private void EnsureDefaultGroup()
        {
            try
            {
                using var command = _connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR IGNORE INTO Groups (Name, CreatedAt)
                    VALUES ('收藏', datetime('now'));
                ";
                command.ExecuteNonQuery();
            }
            catch
            {
            }
        }

        private void EnsureGroupIdColumn()
        {
            try
            {
                using var command = _connection.CreateCommand();
                command.CommandText = @"
                    PRAGMA table_info(ClipboardItems);
                ";
                
                bool hasGroupIdColumn = false;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string? columnName = reader["name"]?.ToString();
                    if (columnName == "GroupId")
                    {
                        hasGroupIdColumn = true;
                        break;
                    }
                }
                
                if (!hasGroupIdColumn)
                {
                    using var alterCommand = _connection.CreateCommand();
                    alterCommand.CommandText = @"
                        ALTER TABLE ClipboardItems ADD COLUMN GroupId INTEGER DEFAULT NULL;
                    ";
                    alterCommand.ExecuteNonQuery();
                }
            }
            catch
            {
            }
        }

        public long InsertItem(Models.ClipboardItem item)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ClipboardItems (Type, Content, ImagePath, ImageHash, FilePaths, CreatedAt, PreviewText, GroupId)
                VALUES (@Type, @Content, @ImagePath, @ImageHash, @FilePaths, @CreatedAt, @PreviewText, @GroupId);
                SELECT last_insert_rowid();
            ";
            command.Parameters.AddWithValue("@Type", (int)item.Type);
            command.Parameters.AddWithValue("@Content", item.Content ?? "");
            command.Parameters.AddWithValue("@ImagePath", item.ImagePath ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ImageHash", item.ImageHash ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@FilePaths", string.Join("|", item.FilePaths));
            command.Parameters.AddWithValue("@CreatedAt", item.CreatedAt);
            command.Parameters.AddWithValue("@PreviewText", item.PreviewText);
            command.Parameters.AddWithValue("@GroupId", item.GroupId ?? (object)DBNull.Value);

            return Convert.ToInt64(command.ExecuteScalar());
        }

        public List<Models.ClipboardItem> GetItems(int limit = 100, int offset = 0, string? searchText = null, 
            int? typeFilter = null, long? groupIdFilter = null)
        {
            var items = new List<Models.ClipboardItem>();
            using var command = _connection.CreateCommand();

            var whereClause = new StringBuilder("WHERE 1=1");
            var parameters = new List<SqliteParameter>();

            if (!string.IsNullOrEmpty(searchText))
            {
                whereClause.Append(" AND (Content LIKE @Search OR PreviewText LIKE @Search)");
                parameters.Add(new SqliteParameter("@Search", $"%{searchText}%"));
            }

            if (typeFilter.HasValue)
            {
                whereClause.Append(" AND Type = @Type");
                parameters.Add(new SqliteParameter("@Type", typeFilter.Value));
            }

            if (groupIdFilter.HasValue)
            {
                whereClause.Append(" AND GroupId = @GroupId");
                parameters.Add(new SqliteParameter("@GroupId", groupIdFilter.Value));
            }

            command.CommandText = $@"
                SELECT c.Id, c.Type, c.Content, c.ImagePath, c.ImageHash, c.FilePaths, c.CreatedAt, c.PreviewText, c.GroupId, g.Name as GroupName
                FROM ClipboardItems c
                LEFT JOIN Groups g ON c.GroupId = g.Id
                {whereClause}
                ORDER BY c.CreatedAt DESC
                LIMIT @Limit OFFSET @Offset
            ";
            command.Parameters.AddWithValue("@Limit", limit);
            command.Parameters.AddWithValue("@Offset", offset);
            parameters.ForEach(p => command.Parameters.Add(p));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                items.Add(ReadItem(reader));
            }

            return items;
        }

        public List<Models.ClipboardItem> GetAllItems()
        {
            var items = new List<Models.ClipboardItem>();
            using var command = _connection.CreateCommand();

            command.CommandText = @"
                SELECT c.Id, c.Type, c.Content, c.ImagePath, c.ImageHash, c.FilePaths, c.CreatedAt, c.PreviewText, c.GroupId, g.Name as GroupName
                FROM ClipboardItems c
                LEFT JOIN Groups g ON c.GroupId = g.Id
                ORDER BY c.CreatedAt DESC
            ";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                items.Add(ReadItem(reader));
            }

            return items;
        }

        public int GetItemCount(string? searchText = null, int? typeFilter = null)
        {
            using var command = _connection.CreateCommand();

            var whereClause = new StringBuilder("WHERE 1=1");
            var parameters = new List<SqliteParameter>();

            if (!string.IsNullOrEmpty(searchText))
            {
                whereClause.Append(" AND (Content LIKE @Search OR PreviewText LIKE @Search)");
                parameters.Add(new SqliteParameter("@Search", $"%{searchText}%"));
            }

            if (typeFilter.HasValue)
            {
                whereClause.Append(" AND Type = @Type");
                parameters.Add(new SqliteParameter("@Type", typeFilter.Value));
            }

            command.CommandText = $"SELECT COUNT(*) FROM ClipboardItems {whereClause}";
            parameters.ForEach(p => command.Parameters.Add(p));

            return Convert.ToInt32(command.ExecuteScalar());
        }

        public Models.ClipboardItem? GetItem(long id)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT c.Id, c.Type, c.Content, c.ImagePath, c.ImageHash, c.FilePaths, c.CreatedAt, c.PreviewText, c.GroupId, g.Name as GroupName
                FROM ClipboardItems c
                LEFT JOIN Groups g ON c.GroupId = g.Id
                WHERE c.Id = @Id
            ";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadItem(reader);
            }

            return null;
        }

        public void DeleteItem(long id)
        {
            // 先获取项信息，以便删除对应的图片文件
            var item = GetItem(id);
            if (item != null && item.Type == Models.ClipboardType.Image && !string.IsNullOrEmpty(item.ImagePath))
            {
                try
                {
                    // 构建完整的文件路径
                    string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, item.ImagePath);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
                catch
                {
                }
            }
            
            // 从数据库中删除项
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM ClipboardItems WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }

        public void DeleteOldItems(DateTime cutoffDate)
        {
            // 获取要删除的图片项（仅删除未分组的记录）
            using var selectCommand = _connection.CreateCommand();
            selectCommand.CommandText = "SELECT Id, ImagePath FROM ClipboardItems WHERE CreatedAt < @Cutoff AND Type = @Type AND GroupId IS NULL";
            selectCommand.Parameters.AddWithValue("@Cutoff", cutoffDate);
            selectCommand.Parameters.AddWithValue("@Type", (int)Models.ClipboardType.Image);
            
            using var reader = selectCommand.ExecuteReader();
            var imagePaths = new List<string>();
            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                {
                    string imagePath = reader.GetString(1);
                    imagePaths.Add(imagePath);
                }
            }
            
            // 删除对应的图片文件
            foreach (string imagePath in imagePaths)
            {
                try
                {
                    string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, imagePath);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
                catch
                {
                }
            }
            
            // 从数据库中删除项（仅删除未分组的记录）
            using var deleteCommand = _connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM ClipboardItems WHERE CreatedAt < @Cutoff AND GroupId IS NULL";
            deleteCommand.Parameters.AddWithValue("@Cutoff", cutoffDate);
            deleteCommand.ExecuteNonQuery();
        }

        public void ClearHistory()
        {
            // 获取要删除的图片项
            using var selectCommand = _connection.CreateCommand();
            selectCommand.CommandText = "SELECT Id, ImagePath FROM ClipboardItems WHERE Type = @Type";
            selectCommand.Parameters.AddWithValue("@Type", (int)Models.ClipboardType.Image);

            using var reader = selectCommand.ExecuteReader();
            var imagePaths = new List<string>();
            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                {
                    string imagePath = reader.GetString(1);
                    imagePaths.Add(imagePath);
                }
            }

            // 删除对应的图片文件
            foreach (string imagePath in imagePaths)
            {
                try
                {
                    string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, imagePath);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
                catch
                {
                }
            }

            // 从数据库中删除项
            using var deleteCommand = _connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM ClipboardItems";
            deleteCommand.ExecuteNonQuery();
        }

        public void ClearUngroupedHistory()
        {
            // 获取要删除的未分组图片项
            using var selectCommand = _connection.CreateCommand();
            selectCommand.CommandText = "SELECT Id, ImagePath FROM ClipboardItems WHERE Type = @Type AND GroupId IS NULL";
            selectCommand.Parameters.AddWithValue("@Type", (int)Models.ClipboardType.Image);

            using var reader = selectCommand.ExecuteReader();
            var imagePaths = new List<string>();
            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                {
                    string imagePath = reader.GetString(1);
                    imagePaths.Add(imagePath);
                }
            }

            // 删除对应的图片文件
            foreach (string imagePath in imagePaths)
            {
                try
                {
                    string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, imagePath);
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
                catch
                {
                }
            }

            // 从数据库中删除未分组的项
            using var deleteCommand = _connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM ClipboardItems WHERE GroupId IS NULL";
            deleteCommand.ExecuteNonQuery();
        }

        public bool ItemExists(string content, int type)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM ClipboardItems WHERE Content = @Content AND Type = @Type AND CreatedAt > @RecentTime";
            command.Parameters.AddWithValue("@Content", content);
            command.Parameters.AddWithValue("@Type", type);
            command.Parameters.AddWithValue("@RecentTime", DateTime.Now.AddSeconds(-5));
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        public void DeleteDuplicateItems(string content, int type)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM ClipboardItems WHERE Content = @Content AND Type = @Type";
            command.Parameters.AddWithValue("@Content", content);
            command.Parameters.AddWithValue("@Type", type);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 检查内容是否已存在，返回 (是否存在, 是否已分组)
        /// </summary>
        public (bool exists, bool isGrouped) CheckContentExists(string content, int type)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT GroupId IS NOT NULL as IsGrouped 
                FROM ClipboardItems 
                WHERE Content = @Content AND Type = @Type
                LIMIT 1";
            command.Parameters.AddWithValue("@Content", content);
            command.Parameters.AddWithValue("@Type", type);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var isGrouped = reader.GetBoolean(0);
                return (true, isGrouped);
            }
            return (false, false);
        }

        /// <summary>
        /// 删除未分组的重复项，保留已分组的
        /// </summary>
        public int DeleteUngroupedDuplicates(string content, int type)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM ClipboardItems 
                WHERE Content = @Content AND Type = @Type AND GroupId IS NULL;
                SELECT changes();";
            command.Parameters.AddWithValue("@Content", content);
            command.Parameters.AddWithValue("@Type", type);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        public List<Models.Group> GetAllGroups()
        {
            var groups = new List<Models.Group>();
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, CreatedAt
                FROM Groups
                ORDER BY CreatedAt ASC
            ";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                groups.Add(new Models.Group
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    CreatedAt = reader.GetDateTime(2)
                });
            }

            return groups;
        }

        public long CreateGroup(string name)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Groups (Name, CreatedAt)
                VALUES (@Name, @CreatedAt);
                SELECT last_insert_rowid();
            ";
            command.Parameters.AddWithValue("@Name", name);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

            return Convert.ToInt64(command.ExecuteScalar());
        }

        public void UpdateGroup(long id, string name)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "UPDATE Groups SET Name = @Name WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Name", name);
            command.ExecuteNonQuery();
        }

        public void DeleteGroup(long id)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM Groups WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }

        public string? GetGroupName(long? groupId)
        {
            if (!groupId.HasValue)
                return null;

            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT Name FROM Groups WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", groupId.Value);
            var result = command.ExecuteScalar();
            return result?.ToString();
        }

        public void UpdateItemGroup(long itemId, long? groupId)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "UPDATE ClipboardItems SET GroupId = @GroupId WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", itemId);
            command.Parameters.AddWithValue("@GroupId", groupId ?? (object)DBNull.Value);
            command.ExecuteNonQuery();
        }

        public void UpdateItemContent(long itemId, string content)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                UPDATE ClipboardItems 
                SET Content = @Content, 
                    PreviewText = @PreviewText 
                WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", itemId);
            command.Parameters.AddWithValue("@Content", content);
            command.Parameters.AddWithValue("@PreviewText", content.Length > 100 ? content.Substring(0, 100) : content);
            command.ExecuteNonQuery();
        }

        public bool ImageExistsInDatabase(string imageHash)
        {
            try
            {
                using var command = _connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM ClipboardItems WHERE ImageHash = @ImageHash";
                command.Parameters.AddWithValue("@ImageHash", imageHash);
                int count = Convert.ToInt32(command.ExecuteScalar());
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        public void CleanupExcessHistoryItems(int maxItems)
        {
            if (maxItems <= 0) // 0或负数表示无限
                return;

            // 获取当前未分组记录的数量
            using var countCommand = _connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM ClipboardItems WHERE GroupId IS NULL";
            int currentCount = Convert.ToInt32(countCommand.ExecuteScalar());

            // 如果超出限制，删除最早的记录
            if (currentCount > maxItems)
            {
                int itemsToDelete = currentCount - maxItems;

                // 获取要删除的记录ID和图片路径
                using var selectCommand = _connection.CreateCommand();
                selectCommand.CommandText = @"
                    SELECT Id, ImagePath 
                    FROM ClipboardItems 
                    WHERE GroupId IS NULL 
                    ORDER BY CreatedAt ASC 
                    LIMIT @Limit";
                selectCommand.Parameters.AddWithValue("@Limit", itemsToDelete);

                using var reader = selectCommand.ExecuteReader();
                var itemsToDeleteList = new List<(long Id, string? ImagePath)>();
                while (reader.Read())
                {
                    long id = reader.GetInt64(0);
                    string? imagePath = reader.IsDBNull(1) ? null : reader.GetString(1);
                    itemsToDeleteList.Add((id, imagePath));
                }

                // 删除对应的图片文件
                foreach (var (id, imagePath) in itemsToDeleteList)
                {
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        try
                        {
                            string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, imagePath);
                            if (System.IO.File.Exists(fullPath))
                            {
                                System.IO.File.Delete(fullPath);
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                // 从数据库中删除记录
                if (itemsToDeleteList.Count > 0)
                {
                    var idsToDelete = string.Join(",", itemsToDeleteList.Select(r => r.Id));
                    using var deleteCommand = _connection.CreateCommand();
                    deleteCommand.CommandText = $"DELETE FROM ClipboardItems WHERE Id IN ({idsToDelete})";
                    deleteCommand.ExecuteNonQuery();
                }
            }
        }

        private Models.ClipboardItem ReadItem(SqliteDataReader reader)
        {
            var filePathsStr = reader["FilePaths"]?.ToString() ?? "";
            var filePaths = string.IsNullOrEmpty(filePathsStr)
                ? new List<string>()
                : new List<string>(filePathsStr.Split('|').Where(s => !string.IsNullOrEmpty(s)));

            return new Models.ClipboardItem
            {
                Id = reader.GetInt64(0),
                Type = (Models.ClipboardType)reader.GetInt32(1),
                Content = reader["Content"]?.ToString() ?? "",
                ImagePath = reader["ImagePath"]?.ToString(),
                ImageHash = reader["ImageHash"]?.ToString(),
                FilePaths = filePaths,
                CreatedAt = reader.GetDateTime(6),
                PreviewText = reader["PreviewText"]?.ToString() ?? "",
                GroupId = reader.IsDBNull(8) ? (long?)null : reader.GetInt64(8),
                GroupName = reader.IsDBNull(9) ? null : reader.GetString(9)
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection.Close();
                _connection.Dispose();
                _disposed = true;
            }
        }
    }
}
