using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WinVClip.Models;

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
            // Pooling=False：关闭连接后立即释放 db 文件句柄，
            // 避免备份/还原等直接覆盖 db 文件的操作因连接池持有句柄而失败
            _connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
            _connection.Open();
            RegisterRegexpFunction();
            ConfigurePragmas();
            InitializeDatabase();
        }

        /// <summary>注册 SQLite REGEXP 函数，使 "Content REGEXP @Pattern" 语法可用（正则搜索）</summary>
        private void RegisterRegexpFunction()
        {
            _connection.CreateFunction("regexp", (string? pattern, string? input) =>
            {
                if (pattern == null || input == null)
                    return false;
                try
                {
                    return System.Text.RegularExpressions.Regex.IsMatch(input, pattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                        TimeSpan.FromMilliseconds(100));
                }
                catch (System.Text.RegularExpressions.RegexParseException)
                {
                    return false;
                }
                catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
                {
                    return false;
                }
            });
        }

        private void ConfigurePragmas()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                PRAGMA journal_mode=WAL;
                PRAGMA cache_size=-2000;
                PRAGMA mmap_size=0;
                PRAGMA temp_store=MEMORY;
            ";
            command.ExecuteNonQuery();
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
                CREATE INDEX IF NOT EXISTS idx_content_type ON ClipboardItems(Content, Type);
            ";
            command.ExecuteNonQuery();

            EnsureGroupIdColumn();
            EnsureRichTextColumns();
            EnsureCsvContentColumn();
            EnsureGroupIconColumn();
            EnsurePinyinColumn();
            BackfillPinyinIndex();
            EnsureDefaultGroup();
        }

        /// <summary>拼音索引列（文本/富文本记录的 "全拼 首字母" 小写串，供拼音搜索）</summary>
        private void EnsurePinyinColumn()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"PRAGMA table_info(ClipboardItems);";

            bool hasPinyinColumn = false;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader["name"]?.ToString() == "PinyinIndex")
                {
                    hasPinyinColumn = true;
                    break;
                }
            }

            if (!hasPinyinColumn)
            {
                using var alterCommand = _connection.CreateCommand();
                alterCommand.CommandText = @"ALTER TABLE ClipboardItems ADD COLUMN PinyinIndex TEXT;";
                alterCommand.ExecuteNonQuery();
            }
        }

        /// <summary>为历史记录补算缺失的拼音索引（仅文本/富文本类型，事务批量）</summary>
        private void BackfillPinyinIndex()
        {
            var pending = new List<(long Id, string Content)>();
            using (var command = _connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT Id, Content FROM ClipboardItems
                    WHERE PinyinIndex IS NULL AND Type IN (@TextType, @RichTextType)
                ";
                command.Parameters.AddWithValue("@TextType", (int)Models.ClipboardType.Text);
                command.Parameters.AddWithValue("@RichTextType", (int)Models.ClipboardType.RichText);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.GetInt64(0);
                    var content = reader["Content"]?.ToString();
                    if (!string.IsNullOrEmpty(content))
                        pending.Add((id, content));
                }
            }

            if (pending.Count == 0)
                return;

            using var transaction = _connection.BeginTransaction();
            using var update = _connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = @"UPDATE ClipboardItems SET PinyinIndex = @PinyinIndex WHERE Id = @Id";
            var pinyinParam = update.CreateParameter();
            pinyinParam.ParameterName = "@PinyinIndex";
            update.Parameters.Add(pinyinParam);
            var idParam = update.CreateParameter();
            idParam.ParameterName = "@Id";
            update.Parameters.Add(idParam);

            foreach (var (id, content) in pending)
            {
                pinyinParam.Value = (object?)PinyinService.BuildPinyinIndex(content) ?? DBNull.Value;
                idParam.Value = id;
                update.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        private void EnsureDefaultGroup()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO Groups (Name, Icon, CreatedAt)
                VALUES ('Favorite', '🌟', datetime('now'));
            ";
            command.ExecuteNonQuery();
        }

        private void EnsureGroupIconColumn()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"PRAGMA table_info(Groups);";

            bool hasIconColumn = false;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (reader["name"]?.ToString() == "Icon")
                {
                    hasIconColumn = true;
                    break;
                }
            }

            if (!hasIconColumn)
            {
                using var alterCommand = _connection.CreateCommand();
                alterCommand.CommandText = @"
                    ALTER TABLE Groups ADD COLUMN Icon TEXT NOT NULL DEFAULT '⭐';
                ";
                alterCommand.ExecuteNonQuery();
            }
        }

        private void EnsureGroupIdColumn()
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

        private void EnsureRichTextColumns()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"PRAGMA table_info(ClipboardItems);";

            bool hasRichContentColumn = false;
            bool hasRichFormatColumn = false;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string? columnName = reader["name"]?.ToString();
                if (columnName == "RichContent")
                    hasRichContentColumn = true;
                if (columnName == "RichFormat")
                    hasRichFormatColumn = true;
            }

            if (!hasRichContentColumn)
            {
                using var alterCommand = _connection.CreateCommand();
                alterCommand.CommandText = @"ALTER TABLE ClipboardItems ADD COLUMN RichContent TEXT;";
                alterCommand.ExecuteNonQuery();
            }

            if (!hasRichFormatColumn)
            {
                using var alterCommand = _connection.CreateCommand();
                alterCommand.CommandText = @"ALTER TABLE ClipboardItems ADD COLUMN RichFormat TEXT;";
                alterCommand.ExecuteNonQuery();
            }
        }

        private void EnsureCsvContentColumn()
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"PRAGMA table_info(ClipboardItems);";

            bool hasCsvContentColumn = false;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string? columnName = reader["name"]?.ToString();
                if (columnName == "CsvContent")
                {
                    hasCsvContentColumn = true;
                    break;
                }
            }

            if (!hasCsvContentColumn)
            {
                using var alterCommand = _connection.CreateCommand();
                alterCommand.CommandText = @"ALTER TABLE ClipboardItems ADD COLUMN CsvContent TEXT;";
                alterCommand.ExecuteNonQuery();
            }
        }

        private T ExecuteWithRetry<T>(Func<T> action, T defaultValue, int maxRetries = 3)
        {
            int retryCount = maxRetries;
            while (retryCount > 0)
            {
                try
                {
                    return action();
                }
                catch
                {
                    retryCount--;
                    if (retryCount == 0)
                        return defaultValue;
                    System.Threading.Thread.Sleep(10);
                }
            }
            return defaultValue;
        }

        private int ExecuteNonQueryWithRetry(string commandText, Action<SqliteCommand> configureParams, int maxRetries = 3)
        {
            return ExecuteWithRetry(() =>
            {
                using var command = _connection.CreateCommand();
                command.CommandText = commandText;
                configureParams(command);
                return command.ExecuteNonQuery();
            }, 0, maxRetries);
        }

        private T ExecuteScalarWithRetry<T>(string commandText, Action<SqliteCommand> configureParams, T defaultValue, int maxRetries = 3)
        {
            return ExecuteWithRetry(() =>
            {
                using var command = _connection.CreateCommand();
                command.CommandText = commandText;
                configureParams(command);
                var result = command.ExecuteScalar();
                return result == null ? defaultValue : (T)Convert.ChangeType(result, typeof(T));
            }, defaultValue, maxRetries);
        }

        public long InsertItem(Models.ClipboardItem item)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ClipboardItems (Type, Content, RichContent, RichFormat, CsvContent, ImagePath, ImageHash, FilePaths, CreatedAt, PreviewText, GroupId, PinyinIndex)
                VALUES (@Type, @Content, @RichContent, @RichFormat, @CsvContent, @ImagePath, @ImageHash, @FilePaths, @CreatedAt, @PreviewText, @GroupId, @PinyinIndex);
                SELECT last_insert_rowid();
            ";
            command.Parameters.AddWithValue("@Type", (int)item.Type);
            command.Parameters.AddWithValue("@Content", item.Content ?? "");
            command.Parameters.AddWithValue("@RichContent", item.RichContent ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@RichFormat", item.RichFormat ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@CsvContent", item.CsvContent ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ImagePath", item.ImagePath ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ImageHash", item.ImageHash ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@FilePaths", string.Join("|", item.FilePaths));
            command.Parameters.AddWithValue("@CreatedAt", item.CreatedAt);
            command.Parameters.AddWithValue("@PreviewText", item.PreviewText);
            command.Parameters.AddWithValue("@GroupId", item.GroupId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@PinyinIndex",
                item.Type == Models.ClipboardType.Text || item.Type == Models.ClipboardType.RichText
                    ? (object?)PinyinService.BuildPinyinIndex(item.Content) ?? DBNull.Value
                    : DBNull.Value);

            return Convert.ToInt64(command.ExecuteScalar());
        }

        public int InsertItemsBatch(List<Models.ClipboardItem> items)
        {
            if (items == null || items.Count == 0)
                return 0;

            using var transaction = _connection.BeginTransaction();
            try
            {
                using var command = _connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO ClipboardItems (Type, Content, RichContent, RichFormat, CsvContent, ImagePath, ImageHash, FilePaths, CreatedAt, PreviewText, GroupId, PinyinIndex)
                    VALUES (@Type, @Content, @RichContent, @RichFormat, @CsvContent, @ImagePath, @ImageHash, @FilePaths, @CreatedAt, @PreviewText, @GroupId, @PinyinIndex);
                ";

                var typeParam = command.CreateParameter();
                typeParam.ParameterName = "@Type";
                command.Parameters.Add(typeParam);

                var contentParam = command.CreateParameter();
                contentParam.ParameterName = "@Content";
                command.Parameters.Add(contentParam);

                var richContentParam = command.CreateParameter();
                richContentParam.ParameterName = "@RichContent";
                command.Parameters.Add(richContentParam);

                var richFormatParam = command.CreateParameter();
                richFormatParam.ParameterName = "@RichFormat";
                command.Parameters.Add(richFormatParam);

                var csvContentParam = command.CreateParameter();
                csvContentParam.ParameterName = "@CsvContent";
                command.Parameters.Add(csvContentParam);

                var imagePathParam = command.CreateParameter();
                imagePathParam.ParameterName = "@ImagePath";
                command.Parameters.Add(imagePathParam);

                var imageHashParam = command.CreateParameter();
                imageHashParam.ParameterName = "@ImageHash";
                command.Parameters.Add(imageHashParam);

                var filePathsParam = command.CreateParameter();
                filePathsParam.ParameterName = "@FilePaths";
                command.Parameters.Add(filePathsParam);

                var createdAtParam = command.CreateParameter();
                createdAtParam.ParameterName = "@CreatedAt";
                command.Parameters.Add(createdAtParam);

                var previewTextParam = command.CreateParameter();
                previewTextParam.ParameterName = "@PreviewText";
                command.Parameters.Add(previewTextParam);

                var groupIdParam = command.CreateParameter();
                groupIdParam.ParameterName = "@GroupId";
                command.Parameters.Add(groupIdParam);

                var pinyinIndexParam = command.CreateParameter();
                pinyinIndexParam.ParameterName = "@PinyinIndex";
                command.Parameters.Add(pinyinIndexParam);

                foreach (var item in items)
                {
                    typeParam.Value = (int)item.Type;
                    contentParam.Value = item.Content ?? "";
                    richContentParam.Value = item.RichContent ?? (object)DBNull.Value;
                    richFormatParam.Value = item.RichFormat ?? (object)DBNull.Value;
                    csvContentParam.Value = item.CsvContent ?? (object)DBNull.Value;
                    imagePathParam.Value = item.ImagePath ?? (object)DBNull.Value;
                    imageHashParam.Value = item.ImageHash ?? (object)DBNull.Value;
                    filePathsParam.Value = string.Join("|", item.FilePaths ?? new List<string>());
                    createdAtParam.Value = item.CreatedAt;
                    previewTextParam.Value = item.PreviewText ?? "";
                    groupIdParam.Value = item.GroupId ?? (object)DBNull.Value;
                    pinyinIndexParam.Value =
                        item.Type == Models.ClipboardType.Text || item.Type == Models.ClipboardType.RichText
                            ? (object?)PinyinService.BuildPinyinIndex(item.Content) ?? DBNull.Value
                            : DBNull.Value;

                    command.ExecuteNonQuery();
                }

                transaction.Commit();
                return items.Count;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// 构建搜索 WHERE 子句（统一供 GetItems/GetTotalCount/DeleteItemsByFilter 使用）。
        /// 支持普通分词（默认命中任一关键词）、精确模式（全部命中）、正则、通配符（* ?）与拼音索引匹配。
        /// </summary>
        private static void AppendSearchClause(StringBuilder whereClause, List<SqliteParameter> parameters,
            string? searchText, Models.SearchOptions? options)
        {
            if (string.IsNullOrEmpty(searchText))
                return;

            var opts = options ?? new Models.SearchOptions();

            // 正则模式（无效正则退回整词 LIKE）
            if (opts.UseRegex)
            {
                try
                {
                    _ = new System.Text.RegularExpressions.Regex(searchText,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                        TimeSpan.FromMilliseconds(100));
                    whereClause.Append(" AND (Content REGEXP @SearchRegex OR PreviewText REGEXP @SearchRegex)");
                    parameters.Add(new SqliteParameter("@SearchRegex", searchText));
                }
                catch (ArgumentException)
                {
                    whereClause.Append(" AND (Content LIKE @SearchKw0 OR PreviewText LIKE @SearchKw0)");
                    parameters.Add(new SqliteParameter("@SearchKw0", $"%{searchText}%"));
                }
                return;
            }

            // 通配符模式：* → %、? → _，其余 LIKE 特殊符转义
            if (opts.UseWildcard)
            {
                whereClause.Append(" AND (Content LIKE @SearchWild ESCAPE '\\' OR PreviewText LIKE @SearchWild ESCAPE '\\')");
                parameters.Add(new SqliteParameter("@SearchWild", ConvertWildcardToLike(searchText)));
                return;
            }

            // 普通/精确模式：空格分词
            var keywords = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (keywords.Length == 0)
                keywords = new[] { searchText };

            var clauses = new List<string>();
            for (int i = 0; i < keywords.Length; i++)
            {
                var paramName = $"@SearchKw{i}";
                var group = $"(Content LIKE {paramName} OR PreviewText LIKE {paramName}";
                if (opts.UsePinyin)
                    group += $" OR PinyinIndex LIKE {paramName}";
                group += ")";
                parameters.Add(new SqliteParameter(paramName, $"%{keywords[i]}%"));
                clauses.Add(group);
            }

            whereClause.Append(" AND ").Append(opts.ExactMode
                ? string.Join(" AND ", clauses)
                : string.Join(" OR ", clauses));
        }

        /// <summary>通配符转 LIKE 模式：* → %、? → _，其余特殊字符转义（配 ESCAPE '\\'）</summary>
        private static string ConvertWildcardToLike(string input)
        {
            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                switch (c)
                {
                    case '*': sb.Append('%'); break;
                    case '?': sb.Append('_'); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '%': sb.Append("\\%"); break;
                    case '_': sb.Append("\\_"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 构建类型与分组过滤 WHERE 子句（统一供列表查询、计数与删除使用）。
        /// 文本筛选同时命中富文本记录；groupIdFilter 为 null 表示不限分组。
        /// </summary>
        private static void AppendFilterClause(StringBuilder whereClause, List<SqliteParameter> parameters,
            int? typeFilter, long? groupIdFilter)
        {
            if (typeFilter.HasValue)
            {
                if (typeFilter.Value == (int)Models.ClipboardType.Text)
                {
                    whereClause.Append(" AND (Type = @Type OR Type = @RichTextType)");
                    parameters.Add(new SqliteParameter("@Type", typeFilter.Value));
                    parameters.Add(new SqliteParameter("@RichTextType", (int)Models.ClipboardType.RichText));
                }
                else
                {
                    whereClause.Append(" AND Type = @Type");
                    parameters.Add(new SqliteParameter("@Type", typeFilter.Value));
                }
            }

            if (groupIdFilter.HasValue)
            {
                whereClause.Append(" AND GroupId = @GroupId");
                parameters.Add(new SqliteParameter("@GroupId", groupIdFilter.Value));
            }
        }

        /// <summary>为 IN 查询批量绑定参数并返回占位符名称（@前缀0、@前缀1 …）。</summary>
        private static List<string> AddInParameters(SqliteCommand command, IEnumerable<string> values, string paramPrefix)
        {
            var placeholders = new List<string>();
            var paramIndex = 0;
            foreach (var value in values)
            {
                var paramName = $"@{paramPrefix}{paramIndex++}";
                placeholders.Add(paramName);
                command.Parameters.AddWithValue(paramName, value);
            }
            return placeholders;
        }

        public List<Models.ClipboardItem> GetItems(int limit = 100, int offset = 0, string? searchText = null,
            int? typeFilter = null, long? groupIdFilter = null, Models.SearchOptions? searchOptions = null)
        {
            var items = new List<Models.ClipboardItem>();
            using var command = _connection.CreateCommand();

            var whereClause = new StringBuilder("WHERE 1=1");
            var parameters = new List<SqliteParameter>();

            AppendSearchClause(whereClause, parameters, searchText, searchOptions);
            AppendFilterClause(whereClause, parameters, typeFilter, groupIdFilter);

            command.CommandText = $@"
                SELECT c.Id, c.Type, c.Content, c.RichContent, c.RichFormat, c.CsvContent, c.ImagePath, c.ImageHash, c.FilePaths, c.CreatedAt, c.PreviewText, c.GroupId, g.Name as GroupName
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
            return GetItems(int.MaxValue, 0);
        }

        public int GetTotalCount(string? searchText = null, int? typeFilter = null, long? groupIdFilter = null, Models.SearchOptions? searchOptions = null)
        {
            using var command = _connection.CreateCommand();

            var whereClause = new StringBuilder("WHERE 1=1");
            var parameters = new List<SqliteParameter>();

            AppendSearchClause(whereClause, parameters, searchText, searchOptions);
            AppendFilterClause(whereClause, parameters, typeFilter, groupIdFilter);

            command.CommandText = $@"
                SELECT COUNT(*) FROM ClipboardItems
                {whereClause}
            ";
            parameters.ForEach(p => command.Parameters.Add(p));

            var result = command.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        /// <summary>
        /// 链接分组专用查询：SQL 层用与 IsLink 共用同一正则过滤文本/富文本记录，
        /// 避免全表拉入内存逐条过滤；TRIM 与 IsLink 的修剪行为一致。
        /// </summary>
        public List<Models.ClipboardItem> GetLinkItems(int limit, int offset, string? searchText,
            long? groupIdFilter = null, Models.SearchOptions? searchOptions = null)
        {
            var items = new List<Models.ClipboardItem>();
            using var command = _connection.CreateCommand();

            var whereClause = new StringBuilder("WHERE 1=1");
            var parameters = new List<SqliteParameter>();
            AppendFilterClause(whereClause, parameters, (int)Models.ClipboardType.Text, groupIdFilter);
            AppendLinkClause(whereClause, parameters);
            AppendSearchClause(whereClause, parameters, searchText, searchOptions);

            command.CommandText = $@"
                SELECT c.Id, c.Type, c.Content, c.RichContent, c.RichFormat, c.CsvContent, c.ImagePath, c.ImageHash, c.FilePaths, c.CreatedAt, c.PreviewText, c.GroupId, g.Name as GroupName
                FROM ClipboardItems c
                LEFT JOIN Groups g ON c.GroupId = g.Id
                {whereClause}
                ORDER BY c.CreatedAt DESC
                LIMIT @Limit OFFSET @Offset
            ";
            // 必须用 AddWithValue：SqliteParameter 的 (string, object) 与 (string, SqliteType) 重载有歧义，
            // 常量枚举值会隐式匹配后者导致参数未赋值
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

        /// <summary>链接分组记录总数（与 GetLinkItems 同一过滤条件）</summary>
        public int GetLinkTotalCount(string? searchText,
            long? groupIdFilter = null, Models.SearchOptions? searchOptions = null)
        {
            using var command = _connection.CreateCommand();

            var whereClause = new StringBuilder("WHERE 1=1");
            var parameters = new List<SqliteParameter>();
            AppendFilterClause(whereClause, parameters, (int)Models.ClipboardType.Text, groupIdFilter);
            AppendLinkClause(whereClause, parameters);
            AppendSearchClause(whereClause, parameters, searchText, searchOptions);

            command.CommandText = $@"
                SELECT COUNT(*) FROM ClipboardItems
                {whereClause}
            ";
            parameters.ForEach(p => command.Parameters.Add(p));

            var result = command.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        /// <summary>
        /// 常见顶级域名白名单（刻意不含 md/py/rs/pl/cs/go/sh 等易与文件扩展名混淆的 ccTLD）。
        /// 需要支持新的 TLD 时直接在此追加即可，其余逻辑无需改动。
        /// </summary>
        private const string CommonTlds =
            "com|net|org|edu|gov|mil|int|info|biz|pro|name|mobi|asia|xyz|top|site|shop|store|online|tech|cloud|blog|wiki|club|live|life|link|work|space|world|today|news|media|design|studio|agency|digital|network|solutions|software|mobile|email|group|team|center|city|zone|vip|win|page|plus|app|dev|io|ai|co|me|tv|cc|cn|tw|hk|mo|jp|kr|sg|my|th|vn|ph|id|in|pk|au|nz|uk|de|fr|it|es|nl|be|ch|at|se|no|dk|fi|cz|ru|ua|tr|ir|sa|ae|il|eg|za|br|mx|ar|cl|ca|us|eu|ly|to|gg";

        /// <summary>
        /// 链接匹配核心：可选的 http(s) 协议头 + IPv4 或域名 + 可选端口 + 可选路径/查询，(?i) 忽略大小写。
        /// 域名末段必须是白名单中的 TLD 且后随分隔符，因此 x.completed、readme.txt 这类普通文本不会被误判。
        /// 不带行首锚点、可在任意位置匹配，供快捷命令「提取全部网址链接」复用；筛选场景由 LinkPattern 补 ^ 锚点。
        /// </summary>
        internal const string LinkMatchPattern =
            @"(?i)(?:https?://)?(?:(?:\d{1,3}\.){3}\d{1,3}(?![0-9A-Za-z-])|(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+(?:" + CommonTlds + @")(?![A-Za-z0-9-]))(?::\d{1,5})?(?:[/?#]\S*)?";

        /// <summary>
        /// 链接筛选正则：LinkMatchPattern 加行首锚点，要求文本以链接开头（其后可跟随其他内容）。
        /// C# 侧 IsLink 与 SQL 侧 REGEXP 共用同一规则，保证筛选结果一致。
        /// </summary>
        private const string LinkPattern = @"^" + LinkMatchPattern;

        private static readonly System.Text.RegularExpressions.Regex LinkRegex =
            new(LinkPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>判断文本是否为链接（忽略首尾空白）。</summary>
        internal static bool IsLink(string? text)
            => !string.IsNullOrWhiteSpace(text) && LinkRegex.IsMatch(text.Trim());

        private static void AppendLinkClause(StringBuilder whereClause, List<SqliteParameter> parameters)
        {
            whereClause.Append(" AND TRIM(Content) REGEXP @LinkPattern");
            parameters.Add(new SqliteParameter("@LinkPattern", LinkPattern));
        }

        public Models.ClipboardItem? GetItem(long id)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT c.Id, c.Type, c.Content, c.RichContent, c.RichFormat, c.CsvContent, c.ImagePath, c.ImageHash, c.FilePaths, c.CreatedAt, c.PreviewText, c.GroupId, g.Name as GroupName
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
            var item = GetItem(id);

            if (item != null && !string.IsNullOrEmpty(item.ImagePath))
            {
                DeleteImageFile(item.ImagePath);
            }
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM ClipboardItems WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }

        /// <summary>按 Id 批量删除记录（单事务），同步清理关联图片文件，返回实际删除的条数。</summary>
        public int DeleteItemsByIds(IEnumerable<long> ids)
        {
            var idList = ids?.Distinct().ToList();
            if (idList == null || idList.Count == 0) return 0;

            var imagePaths = new List<string>();
            using (var selectCommand = _connection.CreateCommand())
            {
                selectCommand.CommandText = "SELECT ImagePath FROM ClipboardItems WHERE Id = @Id AND ImagePath IS NOT NULL AND ImagePath != ''";
                var selectParam = selectCommand.Parameters.Add("@Id", SqliteType.Integer);
                foreach (var id in idList)
                {
                    selectParam.Value = id;
                    using var reader = selectCommand.ExecuteReader();
                    if (reader.Read() && !reader.IsDBNull(0))
                        imagePaths.Add(reader.GetString(0));
                }
            }

            int deleted = 0;
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    using var deleteCommand = _connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM ClipboardItems WHERE Id = @Id";
                    var deleteParam = deleteCommand.Parameters.Add("@Id", SqliteType.Integer);
                    foreach (var id in idList)
                    {
                        deleteParam.Value = id;
                        deleted += deleteCommand.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }

            DeleteImageFiles(imagePaths);
            if (deleted > 0)
            {
                VacuumDatabase();
            }
            return deleted;
        }

        private void DeleteImageFile(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return;

            string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, imagePath);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        /// <summary>
        /// 清理 images 文件夹中未被任何数据库记录引用的孤儿图片文件。
        /// </summary>
        /// <returns>删除的文件数量</returns>
        public int CleanupOrphanImageFiles()
        {
            try
            {
                string imagesFolder = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "images");
                if (!System.IO.Directory.Exists(imagesFolder))
                    return 0;

                // 收集数据库中仍被引用的图片路径（统一为相对路径、小写、正斜杠比较）
                var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var command = _connection.CreateCommand())
                {
                    command.CommandText = "SELECT ImagePath FROM ClipboardItems WHERE ImagePath IS NOT NULL AND ImagePath != ''";
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        referenced.Add(NormalizeImagePath(reader.GetString(0)));
                    }
                }

                int deletedCount = 0;
                foreach (string file in System.IO.Directory.GetFiles(imagesFolder))
                {
                    string relative = System.IO.Path.Combine("images", System.IO.Path.GetFileName(file));
                    if (!referenced.Contains(NormalizeImagePath(relative)))
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                            deletedCount++;
                        }
                        catch
                        {
                            // 文件被占用等场景跳过，下次启动重试
                        }
                    }
                }

                return deletedCount;
            }
            catch
            {
                return 0;
            }
        }

        private static string NormalizeImagePath(string imagePath)
        {
            return imagePath.Replace('\\', '/').Trim().ToLowerInvariant();
        }

        public int DeleteOldItems(DateTime cutoffDate)
        {
            return DeleteOldItems(cutoffDate, null);
        }

        /// <summary>删除指定时间之前的记录，返回实际删除的条数。</summary>
        public int DeleteOldItems(DateTime cutoffDate, int? typeFilter)
        {
            var deleteIds = new List<int>();
            var imagePaths = new List<string>();

            if (typeFilter == 4)
            {
                string whereClause = "WHERE GroupId IS NULL";
                if (cutoffDate != DateTime.MinValue)
                {
                    whereClause += " AND CreatedAt < @Cutoff";
                }

                using var selectCommand = _connection.CreateCommand();
                selectCommand.CommandText = $"SELECT Id, ImagePath, Content FROM ClipboardItems {whereClause} AND (Type = @TextType OR Type = @RichTextType)";
                if (cutoffDate != DateTime.MinValue)
                {
                    selectCommand.Parameters.AddWithValue("@Cutoff", cutoffDate);
                }
                selectCommand.Parameters.AddWithValue("@TextType", (int)ClipboardType.Text);
                selectCommand.Parameters.AddWithValue("@RichTextType", (int)ClipboardType.RichText);

                using var reader = selectCommand.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(2))
                    {
                        string content = reader.GetString(2);
                        if (IsLink(content))
                        {
                            deleteIds.Add(reader.GetInt32(0));
                            if (!reader.IsDBNull(1))
                            {
                                imagePaths.Add(reader.GetString(1));
                            }
                        }
                    }
                }
            }
            else
            {
                string whereClause = "WHERE GroupId IS NULL";
                if (cutoffDate != DateTime.MinValue)
                {
                    whereClause += " AND CreatedAt < @Cutoff";
                }
                if (typeFilter.HasValue && typeFilter.Value != -1)
                {
                    whereClause += " AND Type = @Type";
                }

                using var selectCommand = _connection.CreateCommand();
                selectCommand.CommandText = $"SELECT Id, ImagePath FROM ClipboardItems {whereClause}";
                if (cutoffDate != DateTime.MinValue)
                {
                    selectCommand.Parameters.AddWithValue("@Cutoff", cutoffDate);
                }
                if (typeFilter.HasValue && typeFilter.Value != -1)
                {
                    selectCommand.Parameters.AddWithValue("@Type", typeFilter.Value);
                }

                using var reader = selectCommand.ExecuteReader();
                while (reader.Read())
                {
                    deleteIds.Add(reader.GetInt32(0));
                    if (!reader.IsDBNull(1))
                    {
                        imagePaths.Add(reader.GetString(1));
                    }
                }
            }

            foreach (string imagePath in imagePaths)
            {
                DeleteImageFile(imagePath);
            }

            if (deleteIds.Count > 0)
            {
                using var deleteCommand = _connection.CreateCommand();
                deleteCommand.CommandText = $"DELETE FROM ClipboardItems WHERE Id IN ({string.Join(",", deleteIds)})";
                deleteCommand.ExecuteNonQuery();
                VacuumDatabase();
            }

            return deleteIds.Count;
        }

        public int ClearHistory()
        {
            return ClearHistoryInternal(ungroupedOnly: false);
        }

        /// <summary>
        /// 删除较早的未分组重复记录：每组重复内容保留最新一条（含已分组记录参与排序），已分组记录本身不删除。
        /// </summary>
        /// <returns>删除的记录数量</returns>
        public int DeleteOldDuplicates()
        {
            try
            {
                int totalDeleted = 0;
                totalDeleted += DeleteOldDuplicatesByType((int)Models.ClipboardType.Text, "IFNULL(Content,'')");
                totalDeleted += DeleteOldDuplicatesByType((int)Models.ClipboardType.RichText,
                    "IFNULL(Content,'') || char(1) || IFNULL(RichContent,'')");
                totalDeleted += DeleteOldDuplicatesByType((int)Models.ClipboardType.FileList, "IFNULL(FilePaths,'')");
                totalDeleted += DeleteOldDuplicatesByType((int)Models.ClipboardType.Image, "IFNULL(ImageHash,'')");

                if (totalDeleted > 0)
                {
                    VacuumDatabase();
                }
                return totalDeleted;
            }
            catch
            {
                return 0;
            }
        }

        private int DeleteOldDuplicatesByType(int type, string partitionKey)
        {
            // 收集待删除记录引用的图片文件
            var imagePaths = new List<string>();
            using (var selectCommand = _connection.CreateCommand())
            {
                selectCommand.CommandText = $@"
                    SELECT ImagePath FROM ClipboardItems
                    WHERE Type = @Type AND GroupId IS NULL
                      AND Id NOT IN (
                          SELECT Id FROM (
                              SELECT Id, ROW_NUMBER() OVER (PARTITION BY {partitionKey}
                                                            ORDER BY CreatedAt DESC, Id DESC) AS rn
                              FROM ClipboardItems WHERE Type = @Type
                          ) WHERE rn = 1)
                      AND ImagePath IS NOT NULL AND ImagePath != ''";
                selectCommand.Parameters.AddWithValue("@Type", type);
                using var reader = selectCommand.ExecuteReader();
                while (reader.Read())
                {
                    imagePaths.Add(reader.GetString(0));
                }
            }

            using var command = _connection.CreateCommand();
            command.CommandText = $@"
                DELETE FROM ClipboardItems
                WHERE Type = @Type AND GroupId IS NULL
                  AND Id NOT IN (
                      SELECT Id FROM (
                          SELECT Id, ROW_NUMBER() OVER (PARTITION BY {partitionKey}
                                                        ORDER BY CreatedAt DESC, Id DESC) AS rn
                          FROM ClipboardItems WHERE Type = @Type
                      ) WHERE rn = 1);
                SELECT changes();";
            command.Parameters.AddWithValue("@Type", type);
            int deleted = Convert.ToInt32(command.ExecuteScalar());

            foreach (string imagePath in imagePaths)
            {
                DeleteImageFile(imagePath);
            }
            return deleted;
        }

        /// <summary>
        /// 删除内容字符数超过指定值的未分组文本/富文本记录。
        /// </summary>
        /// <returns>删除的记录数量</returns>
        public int DeleteLongTextRecords(int minChars)
        {
            if (minChars <= 0)
                return 0;

            try
            {
                var imagePaths = new List<string>();
                using (var selectCommand = _connection.CreateCommand())
                {
                    selectCommand.CommandText = @"
                        SELECT ImagePath FROM ClipboardItems
                        WHERE GroupId IS NULL AND Type IN (@TextType, @RichTextType)
                          AND LENGTH(COALESCE(Content,'')) > @MinChars
                          AND ImagePath IS NOT NULL AND ImagePath != ''";
                    selectCommand.Parameters.AddWithValue("@TextType", (int)Models.ClipboardType.Text);
                    selectCommand.Parameters.AddWithValue("@RichTextType", (int)Models.ClipboardType.RichText);
                    selectCommand.Parameters.AddWithValue("@MinChars", minChars);
                    using var reader = selectCommand.ExecuteReader();
                    while (reader.Read())
                    {
                        imagePaths.Add(reader.GetString(0));
                    }
                }

                using var command = _connection.CreateCommand();
                command.CommandText = @"
                    DELETE FROM ClipboardItems
                    WHERE GroupId IS NULL AND Type IN (@TextType, @RichTextType)
                      AND LENGTH(COALESCE(Content,'')) > @MinChars;
                    SELECT changes();";
                command.Parameters.AddWithValue("@TextType", (int)Models.ClipboardType.Text);
                command.Parameters.AddWithValue("@RichTextType", (int)Models.ClipboardType.RichText);
                command.Parameters.AddWithValue("@MinChars", minChars);
                int deleted = Convert.ToInt32(command.ExecuteScalar());

                foreach (string imagePath in imagePaths)
                {
                    DeleteImageFile(imagePath);
                }

                if (deleted > 0)
                {
                    VacuumDatabase();
                }
                return deleted;
            }
            catch
            {
                return 0;
            }
        }

        public int ClearUngroupedHistory()
        {
            return ClearHistoryInternal(ungroupedOnly: true);
        }

        /// <summary>按当前筛选条件删除记录，返回实际删除的条数。</summary>
        public int DeleteItemsByFilter(long? groupIdFilter, int? typeFilter, string? searchText, bool ungroupedOnly, bool linkOnly = false, Models.SearchOptions? searchOptions = null)
        {
            var whereClause = new StringBuilder("WHERE 1=1");
            var paramNames = new List<string>();
            var paramValues = new List<object>();

            if (ungroupedOnly)
            {
                whereClause.Append(" AND (GroupId IS NULL OR GroupId = 0)");
            }

            if (!string.IsNullOrEmpty(searchText))
            {
                var searchParams = new List<SqliteParameter>();
                AppendSearchClause(whereClause, searchParams, searchText, searchOptions);
                foreach (var p in searchParams)
                {
                    paramNames.Add(p.ParameterName);
                    paramValues.Add(p.Value ?? (object)"");
                }
            }

            if (linkOnly)
            {
                whereClause.Append(" AND (Type = @TypeText OR Type = @TypeRich)");
                whereClause.Append(" AND TRIM(Content) REGEXP @LinkPattern");
                paramNames.Add("@TypeText");
                paramValues.Add((int)Models.ClipboardType.Text);
                paramNames.Add("@TypeRich");
                paramValues.Add((int)Models.ClipboardType.RichText);
                paramNames.Add("@LinkPattern");
                paramValues.Add(LinkPattern);
            }
            else if (typeFilter.HasValue)
            {
                if (typeFilter.Value == (int)Models.ClipboardType.Text)
                {
                    whereClause.Append(" AND (Type = @Type OR Type = @RichTextType)");
                    paramNames.Add("@Type");
                    paramValues.Add(typeFilter.Value);
                    paramNames.Add("@RichTextType");
                    paramValues.Add((int)Models.ClipboardType.RichText);
                }
                else
                {
                    whereClause.Append(" AND Type = @Type");
                    paramNames.Add("@Type");
                    paramValues.Add(typeFilter.Value);
                }
            }

            if (groupIdFilter.HasValue)
            {
                whereClause.Append(" AND GroupId = @GroupId");
                paramNames.Add("@GroupId");
                paramValues.Add(groupIdFilter.Value);
            }

            var imagePaths = new List<string>();
            using (var selectCommand = _connection.CreateCommand())
            {
                selectCommand.CommandText = $@"
                    SELECT Id, ImagePath FROM ClipboardItems
                    {whereClause} AND ImagePath IS NOT NULL AND ImagePath != ''";
                for (int i = 0; i < paramNames.Count; i++)
                    selectCommand.Parameters.AddWithValue(paramNames[i], paramValues[i]);
                using var reader = selectCommand.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(1))
                    {
                        imagePaths.Add(reader.GetString(1));
                    }
                }
            }

            DeleteImageFiles(imagePaths);

            using var deleteCommand = _connection.CreateCommand();
            deleteCommand.CommandText = $"DELETE FROM ClipboardItems {whereClause}";
            for (int i = 0; i < paramNames.Count; i++)
                deleteCommand.Parameters.AddWithValue(paramNames[i], paramValues[i]);
            int deleted = deleteCommand.ExecuteNonQuery();
            VacuumDatabase();
            return deleted;
        }

        private int ClearHistoryInternal(bool ungroupedOnly)
        {
            var imagePaths = GetImagePathsToDelete(ungroupedOnly);
            DeleteImageFiles(imagePaths);
            int deleted = DeleteItemsFromDatabase(ungroupedOnly);
            VacuumDatabase();
            return deleted;
        }

        private List<string> GetImagePathsToDelete(bool ungroupedOnly)
        {
            var imagePaths = new List<string>();
            using var selectCommand = _connection.CreateCommand();

            if (ungroupedOnly)
            {
                selectCommand.CommandText = "SELECT Id, ImagePath FROM ClipboardItems WHERE ImagePath IS NOT NULL AND ImagePath != '' AND GroupId IS NULL";
            }
            else
            {
                selectCommand.CommandText = "SELECT Id, ImagePath FROM ClipboardItems WHERE ImagePath IS NOT NULL AND ImagePath != ''";
            }

            using var reader = selectCommand.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                {
                    imagePaths.Add(reader.GetString(1));
                }
            }
            return imagePaths;
        }

        private void DeleteImageFiles(List<string> imagePaths)
        {
            foreach (string imagePath in imagePaths)
            {
                DeleteImageFile(imagePath);
            }
        }

        private int DeleteItemsFromDatabase(bool ungroupedOnly)
        {
            using var deleteCommand = _connection.CreateCommand();
            deleteCommand.CommandText = ungroupedOnly 
                ? "DELETE FROM ClipboardItems WHERE GroupId IS NULL"
                : "DELETE FROM ClipboardItems";
            return deleteCommand.ExecuteNonQuery();
        }

        public int UpdateDuplicateItemTimestamp(string content, int type)
        {
            return ExecuteNonQueryWithRetry(@"
                UPDATE ClipboardItems 
                SET CreatedAt = @CreatedAt 
                WHERE Content = @Content AND Type = @Type
            ", cmd =>
            {
                cmd.Parameters.AddWithValue("@Content", content);
                cmd.Parameters.AddWithValue("@Type", type);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
            });
        }

        public int UpdateDuplicateRichTextTimestamp(string content, string richContent)
        {
            return ExecuteNonQueryWithRetry(@"
                UPDATE ClipboardItems 
                SET CreatedAt = @CreatedAt 
                WHERE Content = @Content AND RichContent = @RichContent AND Type = @Type
            ", cmd =>
            {
                cmd.Parameters.AddWithValue("@Content", content);
                cmd.Parameters.AddWithValue("@RichContent", richContent);
                cmd.Parameters.AddWithValue("@Type", (int)Models.ClipboardType.RichText);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
            });
        }

        public int UpdateDuplicateImageTimestamp(string imageHash)
        {
            return ExecuteNonQueryWithRetry(@"
                UPDATE ClipboardItems 
                SET CreatedAt = @CreatedAt 
                WHERE ImageHash = @ImageHash
            ", cmd =>
            {
                cmd.Parameters.AddWithValue("@ImageHash", imageHash);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
            });
        }

        public int UpdateItemTimestampById(long id)
        {
            return ExecuteNonQueryWithRetry(@"
                UPDATE ClipboardItems 
                SET CreatedAt = @CreatedAt 
                WHERE Id = @Id
            ", cmd =>
            {
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
            });
        }

        /// <summary>
        /// 查询指定类型下、指定列值命中集合的记录，返回「列值 → 是否已分组」映射。
        /// 参数前缀用于生成 @xx0/@xx1... 占位符；列名与类型由调用方给定。
        /// </summary>
        private Dictionary<string, bool> GetExistingGroupedFlags(ClipboardType type, string column,
            IEnumerable<string>? values, string paramPrefix)
        {
            var result = new Dictionary<string, bool>();
            if (values == null)
                return result;

            using var command = _connection.CreateCommand();
            var placeholders = AddInParameters(command, values, paramPrefix);
            if (placeholders.Count == 0)
                return result;

            command.CommandText = $@"
                SELECT {column}, GroupId IS NOT NULL as IsGrouped 
                FROM ClipboardItems 
                WHERE Type = @Type AND {column} IN ({string.Join(",", placeholders)})";
            command.Parameters.AddWithValue("@Type", (int)type);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result[reader.GetString(0)] = reader.GetBoolean(1);
            }
            return result;
        }

        public Dictionary<string, bool> GetExistingTextContents(HashSet<string> contents)
            => GetExistingGroupedFlags(ClipboardType.Text, "Content",
                contents?.Where(c => !string.IsNullOrWhiteSpace(c)), "C");

        /// <summary>
        /// 删除指定类型下未分组、且指定列值命中集合的记录，返回实际删除行数。
        /// 参数前缀用于生成 @xx0/@xx1... 占位符；列名与类型由调用方给定。
        /// </summary>
        private int DeleteUngroupedColumnDuplicatesBatch(ClipboardType type, string column,
            IEnumerable<string>? values, string paramPrefix)
        {
            if (values == null)
                return 0;

            using var command = _connection.CreateCommand();
            var placeholders = AddInParameters(command, values, paramPrefix);
            if (placeholders.Count == 0)
                return 0;

            command.CommandText = $@"
                DELETE FROM ClipboardItems 
                WHERE Type = @Type AND GroupId IS NULL AND {column} IN ({string.Join(",", placeholders)});
                SELECT changes();";
            command.Parameters.AddWithValue("@Type", (int)type);

            return Convert.ToInt32(command.ExecuteScalar());
        }

        public int DeleteUngroupedDuplicatesBatch(HashSet<string> contents)
            => DeleteUngroupedColumnDuplicatesBatch(ClipboardType.Text, "Content",
                contents?.Where(c => !string.IsNullOrWhiteSpace(c)), "C");

        public Dictionary<string, bool> GetExistingRichTextContents(HashSet<string> richContents)
            => GetExistingGroupedFlags(ClipboardType.RichText, "RichContent", richContents, "R");

        public int DeleteUngroupedRichTextDuplicatesBatch(HashSet<string> richContents)
            => DeleteUngroupedColumnDuplicatesBatch(ClipboardType.RichText, "RichContent", richContents, "R");

        public Dictionary<string, bool> GetExistingFileListSignatures(HashSet<string> signatures)
        {
            var result = new Dictionary<string, bool>();
            if (signatures == null || signatures.Count == 0)
                return result;

            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT FilePaths, GroupId IS NOT NULL as IsGrouped 
                FROM ClipboardItems 
                WHERE Type = @Type";
            command.Parameters.AddWithValue("@Type", (int)ClipboardType.FileList);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var filePathsStr = reader.GetString(0);
                var isGrouped = reader.GetBoolean(1);
                if (!string.IsNullOrEmpty(filePathsStr))
                {
                    var filePaths = filePathsStr.Split('|').ToList();
                    var signature = string.Join("|", filePaths.OrderBy(p => p));
                    if (signatures.Contains(signature))
                    {
                        result[signature] = isGrouped;
                    }
                }
            }
            return result;
        }

        public int DeleteUngroupedFileListDuplicatesBatch(HashSet<string> signatures)
        {
            if (signatures == null || signatures.Count == 0)
                return 0;

            var deletedCount = 0;
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, FilePaths FROM ClipboardItems 
                WHERE Type = @Type AND GroupId IS NULL";
            command.Parameters.AddWithValue("@Type", (int)ClipboardType.FileList);

            var idsToDelete = new List<int>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    var filePathsStr = reader.GetString(1);
                    if (!string.IsNullOrEmpty(filePathsStr))
                    {
                        var filePaths = filePathsStr.Split('|').ToList();
                        var signature = string.Join("|", filePaths.OrderBy(p => p));
                        if (signatures.Contains(signature))
                        {
                            idsToDelete.Add(id);
                        }
                    }
                }
            }

            foreach (var id in idsToDelete)
            {
                using var deleteCommand = _connection.CreateCommand();
                deleteCommand.CommandText = "DELETE FROM ClipboardItems WHERE Id = @Id";
                deleteCommand.Parameters.AddWithValue("@Id", id);
                deleteCommand.ExecuteNonQuery();
                deletedCount++;
            }

            return deletedCount;
        }

        public Dictionary<string, bool> GetExistingImageHashes(HashSet<string> imageHashes)
            => GetExistingGroupedFlags(ClipboardType.Image, "ImageHash", imageHashes, "H");

        public int DeleteUngroupedImageDuplicatesBatch(HashSet<string> imageHashes)
            => DeleteUngroupedColumnDuplicatesBatch(ClipboardType.Image, "ImageHash", imageHashes, "H");

        public List<Models.Group> GetAllGroups()
        {
            var groups = new List<Models.Group>();
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Name, Icon, CreatedAt
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
                    Icon = reader.IsDBNull(2) ? "⭐" : reader.GetString(2),
                    CreatedAt = reader.GetDateTime(3)
                });
            }

            return groups;
        }

        public long CreateGroup(string name, string icon = "⭐")
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Groups (Name, Icon, CreatedAt)
                VALUES (@Name, @Icon, @CreatedAt);
                SELECT last_insert_rowid();
            ";
            command.Parameters.AddWithValue("@Name", name);
            command.Parameters.AddWithValue("@Icon", icon);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

            return Convert.ToInt64(command.ExecuteScalar());
        }

        public long InsertGroup(string name, string icon, DateTime createdAt)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Groups (Name, Icon, CreatedAt)
                VALUES (@Name, @Icon, @CreatedAt);
                SELECT last_insert_rowid();
            ";
            command.Parameters.AddWithValue("@Name", name);
            command.Parameters.AddWithValue("@Icon", icon);
            command.Parameters.AddWithValue("@CreatedAt", createdAt);

            return Convert.ToInt64(command.ExecuteScalar());
        }

        public void UpdateGroup(long id, string name, string icon)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "UPDATE Groups SET Name = @Name, Icon = @Icon WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Name", name);
            command.Parameters.AddWithValue("@Icon", icon);
            command.ExecuteNonQuery();
        }

        public void DeleteGroup(long id)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM Groups WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
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

        public void UpdateItemAsText(long itemId, string content)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = @"
                UPDATE ClipboardItems 
                SET Type = @Type,
                    Content = @Content, 
                    RichContent = NULL,
                    RichFormat = NULL,
                    PreviewText = @PreviewText 
                WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", itemId);
            command.Parameters.AddWithValue("@Type", (int)Models.ClipboardType.Text);
            command.Parameters.AddWithValue("@Content", content);
            command.Parameters.AddWithValue("@PreviewText", content.Length > 100 ? content.Substring(0, 100) : content);
            command.ExecuteNonQuery();
        }

        public bool TextExistsInDatabase(string content)
        {
            return ExecuteScalarWithRetry<int>(
                "SELECT COUNT(*) FROM ClipboardItems WHERE Content = @Content AND Type = @Type",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@Content", content);
                    cmd.Parameters.AddWithValue("@Type", (int)Models.ClipboardType.Text);
                }, 0) > 0;
        }

        public bool RichTextExistsInDatabase(string content, string richContent)
        {
            return ExecuteScalarWithRetry<int>(
                "SELECT COUNT(*) FROM ClipboardItems WHERE Content = @Content AND RichContent = @RichContent AND Type = @Type",
                cmd =>
                {
                    cmd.Parameters.AddWithValue("@Content", content);
                    cmd.Parameters.AddWithValue("@RichContent", richContent);
                    cmd.Parameters.AddWithValue("@Type", (int)Models.ClipboardType.RichText);
                }, 0) > 0;
        }

        public bool FileListExistsByPaths(List<string> filePaths, out string matchingContent)
        {
            matchingContent = null;
            if (filePaths == null || filePaths.Count == 0)
                return false;

            var sortedPaths = filePaths.OrderBy(p => p.ToLowerInvariant()).ToList();
            var normalizedPaths = string.Join("|", sortedPaths);

            var (found, content) = FileListExistsByPathsInternal(sortedPaths, normalizedPaths);
            matchingContent = content;
            return found;
        }

        private (bool found, string content) FileListExistsByPathsInternal(List<string> sortedPaths, string normalizedPaths)
        {
            return ExecuteWithRetry(() =>
            {
                using var command = _connection.CreateCommand();
                command.CommandText = @"
                    SELECT Content, FilePaths FROM ClipboardItems 
                    WHERE Type = @Type
                    ORDER BY CreatedAt DESC
                ";
                command.Parameters.AddWithValue("@Type", (int)Models.ClipboardType.FileList);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var dbContent = reader["Content"]?.ToString() ?? "";
                    var dbFilePaths = reader["FilePaths"]?.ToString() ?? "";

                    var dbPathList = dbFilePaths.Split('|').Where(p => !string.IsNullOrEmpty(p)).ToList();
                    if (dbPathList.Count == sortedPaths.Count)
                    {
                        var sortedDbPaths = dbPathList.OrderBy(p => p.ToLowerInvariant()).ToList();
                        var normalizedDbPaths = string.Join("|", sortedDbPaths);

                        if (normalizedDbPaths == normalizedPaths)
                        {
                            return (true, dbContent);
                        }
                    }
                }
                return (false, (string)null);
            }, (false, (string)null));
        }

        public bool ImageExistsInDatabase(string imageHash)
        {
            return ExecuteScalarWithRetry<int>(
                "SELECT COUNT(*) FROM ClipboardItems WHERE ImageHash = @ImageHash",
                cmd => cmd.Parameters.AddWithValue("@ImageHash", imageHash), 0) > 0;
        }

        public void CleanupExcessHistoryItems(int maxItems)
        {
            if (maxItems <= 0) 
                return;

            using var countCommand = _connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM ClipboardItems WHERE GroupId IS NULL";
            int currentCount = Convert.ToInt32(countCommand.ExecuteScalar());

            if (currentCount > maxItems)
            {
                int itemsToDelete = currentCount - maxItems;

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

                foreach (var (id, imagePath) in itemsToDeleteList)
                {
                    DeleteImageFile(imagePath);
                }

                if (itemsToDeleteList.Count > 0)
                {
                    var idsToDelete = string.Join(",", itemsToDeleteList.Select(r => r.Id));
                    using var deleteCommand = _connection.CreateCommand();
                    deleteCommand.CommandText = $"DELETE FROM ClipboardItems WHERE Id IN ({idsToDelete})";
                    deleteCommand.ExecuteNonQuery();

                    VacuumDatabase();
                }
            }
        }

        public void CheckpointDatabase()
        {
            try
            {
                using var command = _connection.CreateCommand();
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                command.ExecuteNonQuery();
            }
            catch
            {
                // checkpoint 失败不影响数据库读写
            }
        }

        public void VacuumDatabase()
        {
            try
            {

                CheckpointDatabase();

                using var command = _connection.CreateCommand();
                command.CommandText = "VACUUM";
                command.ExecuteNonQuery();

                CheckpointDatabase();
            }
            catch
            {
                // VACUUM 失败不影响后续操作
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
                RichContent = reader["RichContent"]?.ToString(),
                RichFormat = reader["RichFormat"]?.ToString(),
                CsvContent = reader["CsvContent"]?.ToString(),
                ImagePath = reader["ImagePath"]?.ToString(),
                ImageHash = reader["ImageHash"]?.ToString(),
                FilePaths = filePaths,
                CreatedAt = reader.GetDateTime(9),
                PreviewText = reader["PreviewText"]?.ToString() ?? "",
                GroupId = reader.IsDBNull(11) ? (long?)null : reader.GetInt64(11),
                GroupName = reader.IsDBNull(12) ? null : reader.GetString(12)
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
