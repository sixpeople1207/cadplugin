using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mono.Data.Sqlite;
namespace DINNO.DO3D.Database
{
    public partial class SqliteConnection : ConnectionBase, IConnection, IDisposable
    {
        private Mono.Data.Sqlite.SqliteConnection _sqliteConnection = null;
        private Mono.Data.Sqlite.SqliteTransaction _transaction = null;
        private bool _wallOff = false;
        private bool _readOnly = false;
        private bool _disposed = false;
        private Guid _prjID = Guid.Empty;
        private int _prjType = -1;
        private string _prjName = "";
        private int _prjVersion = -1;
        private string _prjDesc = "";
        private string _databaseURI = "";
        public string DatabaseURI
        {
            get
            {
                return _databaseURI;
            }
        }
        public int TransactionCount
        {
            get
            {
                return _transactionCount;
            }
        }
        public bool BeginningTransactions
        {
            get
            {
                return _begin;
            }
        }
        public bool AutoCommit
        {
            get
            {
                return _autoCommit;
            }
            set
            {
                _autoCommit = value;
            }
        }
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }
        public string ConnectionString
        {
            get { return _connectionString; }
            set { _connectionString = value.Trim(); }
        }
        public bool Connected
        {
            get
            {
                return _connected;
            }
        }
        public DatabaseType DatabaseType
        {
            get
            {
                return _dbType;
            }
        }

        public int DBSchemaVersion
        {
            get
            {
                return _dbSchemaVersion;
            }
        }

        public Guid ProjectID 
        {
            get
            {
                return _prjID;
            }
        }

        public string ProjectName 
        {
            get
            {
                return _prjName;
            }
        }
        
        public int ProjectType
        {
            get
            {
                return _prjType;
            }
        }

        public int ProjectVersion
        {
            get
            {
                return _prjVersion;
            }
        }

        public string ProjectDesc
        {
            get
            {
                return _prjDesc;
            }
        }

        public DoNotApplyDBForBatchInternal DoNotApplyDBForBatch
        {
            get
            {
                return new DoNotApplyDBForBatchInternal(this);
            }
        }

        public void Dispose()
        {
            if (_transaction != null)
            {
                _transaction.Rollback();
                _transaction.Dispose();
                _transaction = null;
            }
            if (_sqliteConnection != null)
            {
                close();
                //_sqliteConnection.Close();
                //_sqliteConnection.Dispose();
                //_sqliteConnection = null;
            }
        }

        public SqliteConnection(string name = "", bool wallOff = false, bool readOnly = false)
        {
            _dbType = DatabaseType.Sqlite;
            if (name == "")
                _name = "SQLite Database";

            _wallOff = wallOff;
            _readOnly = readOnly;
        }

        public DbTransaction beginTrans()
        {
            if (!_begin)
            {
                _transaction = _sqliteConnection.BeginTransaction();
                _begin = true;
            }

            return _transaction;
        }

        public void rollbackTrans()
        {
            if (_begin && _transaction != null)
            {
                _transaction.Rollback();
            }
            _begin = false;
            _transactionCount = 0;
            _transaction = null;
        }

        public void commitTrans(bool excuteCommit = false)
        {
            _transactionCount++;
            if ((_autoCommit || excuteCommit) && _begin && _transaction != null)
            {
                _transaction.Commit();
                _transaction = null;
                _begin = false;
                _transactionCount = 0;
            }
        }

        public DataTable getIndexColumns(string tableName)
        {
            return null;// _sqliteConnection.GetSchema(tableName);
        }

        public bool openSQLite(string databaseURI)
        {
            try
            {
                string connectionString = "";

                if (databaseURI.Trim().Equals("") == false)
                {
                    _databaseURI = databaseURI;

                    connectionString = string.Format("Data Source={0}; Pooling=true;", databaseURI);

                    if (_readOnly)
                        connectionString += " Read Only=true;";
                        //connectionString += "Mode=ReadOnly; ";
                }

                return open(connectionString);
            }
            catch (Exception ex)
            {
                throw new Exception("[openSQLite] " + ex.Message, ex);
            }
        }

        public bool open(string connectionString = "")
        {
            try
            {
                if (_connected)
                    close();

                if (connectionString.Trim().Equals("") == false)
                {
                    _connectionString = connectionString.Trim();
                }

                if (_connectionString.Equals("") == true)
                    return false;

                _sqliteConnection = new Mono.Data.Sqlite.SqliteConnection();
                _sqliteConnection.Disposed += SqliteDisposed;
                _sqliteConnection.ConnectionString = _connectionString;
                _sqliteConnection.Open();


                if (_sqliteConnection.State == System.Data.ConnectionState.Open)
                {

                    // EXCLUSIVE
                    using (SqliteCommand cmd = new SqliteCommand("PRAGMA locking_mode=EXCLUSIVE", _sqliteConnection))
                        cmd.ExecuteNonQuery();
                    using (SqliteCommand cmd = new SqliteCommand("PRAGMA cache_size=16000", _sqliteConnection))
                        cmd.ExecuteNonQuery();
                    using (SqliteCommand cmd = new SqliteCommand("PRAGMA page_size=4096", _sqliteConnection))
                        cmd.ExecuteNonQuery();
                    using (SqliteCommand cmd = new SqliteCommand("PRAGMA count_changes=OFF", _sqliteConnection))
                        cmd.ExecuteNonQuery();
                    using (SqliteCommand cmd = new SqliteCommand("PRAGMA synchronous=OFF", _sqliteConnection))
                        cmd.ExecuteNonQuery();
                    if(_wallOff)
                    {
                        using (SqliteCommand cmd = new SqliteCommand("PRAGMA journal_mode=OFF", _sqliteConnection))
                            cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        //using (SqliteCommand cmd = new SqliteCommand("PRAGMA journal_mode=WAL", _sqliteConnection))
                        //    cmd.ExecuteNonQuery();
                    }
                    using (SqliteCommand cmd = new SqliteCommand("PRAGMA temp_store=MEMORY", _sqliteConnection))
                        cmd.ExecuteNonQuery();

                    getProjectInfo();
                    _connected = true;
                    _disposed = false;
                }

                return _connected;
            }
            catch (Exception ex)
            {
                throw new ArgumentException("[SqliteConnection.open] " + ex.Message + "  ConnectionString : " + connectionString);
            }
        }

        private void getProjectInfo()
        {
            try
            {
                using (DbDataReader reader = executeReader(
                    string.Format("SELECT DATABASE_VERSION FROM {0}", TABLE_NAMES.DB_CONFIG)))
                {
                    if(reader.Read())
                    {
                        _dbSchemaVersion = reader.GetInt32(0);
                    }
                }
                using (DbDataReader reader = executeReader(
                    string.Format("SELECT PRJ_ID, PRJ_TYPE, PRJ_NAME, PRJ_VERSION, PRJ_DESC FROM {0}", TABLE_NAMES.PROJECT_ROOT)))
                {
                    if(reader.Read())
                    {
                        _prjID = reader.GetGuid(0);
                        _prjType = reader.GetInt32(1);
                        _prjName = reader.GetString(2);
                        _prjVersion = reader.GetInt32(3);
                        _prjDesc = reader.GetString(4);
                    }
                }
            }
            catch(Exception ex)
            {
                _dbSchemaVersion = 1;
            }
        }

        public void SqliteDisposed(object sender, EventArgs e0)
        {
            _disposed = true;
        }

        public void close()
        {
            if (_sqliteConnection == null)
                return;

            if (false)
            {
                using (SqliteCommand cmd = new SqliteCommand("PRAGMA optimize", _sqliteConnection))
                    cmd.ExecuteNonQuery();
            }

            _sqliteConnection.Disposed += SqliteDisposed;
            while (_sqliteConnection.State != System.Data.ConnectionState.Closed)
            {
                _sqliteConnection.Close();
                System.Threading.Thread.Sleep(100);
            }
            while (!_disposed)
            {
                _sqliteConnection.Dispose();
                System.Threading.Thread.Sleep(100);
            }
            _sqliteConnection.Disposed -= SqliteDisposed;
            _sqliteConnection = null;
            _connected = false;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // SQL문을 실행한 후 어떤 결과값이 돌아오지 않을 때 사용하는 메서드이다.
        // 즉 , 데이터베이스에 데이터값을 넣거나, 데이터를 바꾸고 싶을 때 사용한다.
        // 그래서 UPDATE , DELETE , INSERT 등을 이용할 때 사용된다.
        // 리턴값은 정수형식(INT32)로 반환되며 그 값은 SQL문을 실행했을 때 영향을 받은 행들의 수이다.
        // 그 이외에는 -1 이 반환된다. 
        public int execute(string query)
        {
            int result = 0;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = query;
                result = cmd.ExecuteNonQuery();
            }
            return result;
        }

        // SQL 쿼리 실행 후 첫번째 행의 첫번째 열의 값을 반환한다. 주로 단일값을 가져오는 경우 사용된다.
        public int executeScalar(string query)
        {
            int result = 0;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = query;
                result = Convert.ToInt32(cmd.ExecuteScalar());
            }
            return result;
        }

        public async Task<int> executeAsync(string query)
        {
            int result = 0;

            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = query;
                result = await cmd.ExecuteNonQueryAsync();
            }
            return result;
        }
        public DbDataReader executeReader(string query)
        {
            DbDataReader reader = null;
                
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = query;
                reader = cmd.ExecuteReader();
            }
            return reader;
        }
        public async Task<DbDataReader> executeReaderAsync(string query)
        {
            DbDataReader reader = null;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = query;
                reader = await cmd.ExecuteReaderAsync();
            }
            return reader;
        }
        public DataTable executeDataTable(string query)
        {
            DataTable result = new DataTable();
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = query;
                using (SqliteDataAdapter adapter = new SqliteDataAdapter(cmd))
                {
                    adapter.Fill(result);
                }
            }
            return result;
        }

        public async Task<DataTable> executeDataTableAsync(string query)
        {
            DataTable result = new DataTable();
            using (DbDataReader reader = await executeReaderAsync(query))
            {
                result.Load(reader);
            }
            return result;
        }
        public int updateDataTable(DataTable dt)
        {
            DataColumn[] pc = dt.PrimaryKey;
            if (pc.Length == 0)
                return -1;
            if (dt.Rows.Count == 0)
                return -1;

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("UPDATE {0} SET ", dt.TableName);
            bool bCheck = false;
            foreach (DataColumn c in dt.Columns)
            {
                if (pc.Contains(c) == true)
                    continue;

                if (bCheck)
                    sb.Append(" , ");

                //if (c.ColumnName.Equals("LAST_UPDATE") == true)
                //    sb.Append("LAST_UPDATE = datetime('now','+9 hour')");
                //else if (c.ColumnName.Equals("VERSION_SEQUENCE") == true)
                //    sb.Append("VERSION_SEQUENCE = " + CHANGED_VERSIONSEQUENCE);
                //else
                sb.AppendFormat(" {0} = @{0} ", c.ColumnName);
                bCheck = true;
            }
            sb.Append(" WHERE ");
            bCheck = false;
            foreach (DataColumn c in pc)
            {
                if (bCheck)
                    sb.Append(" AND ");
                sb.AppendFormat(" {0} = @{0} ", c.ColumnName);
            }
            sb.Append(" ");
            //Console.WriteLine(sb.ToString());

            int result = 0;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                cmd.Prepare();
                beginTrans();
                try
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        cmd.Parameters.Clear();
                        foreach (DataColumn col in dt.Columns)
                        {
                            if (pc.Contains(col) == true)
                                continue;

                            if (col.ColumnName.Equals("LAST_UPDATE", StringComparison.CurrentCultureIgnoreCase) == true)
                            {
                                cmd.Parameters.AddWithValue("@" + col.ColumnName, DateTime.Now);
                            }
                            else if (col.ColumnName.Equals("VERSION_SEQUENCE", StringComparison.CurrentCultureIgnoreCase) == true)
                            {
                                int v = int.Parse(CHANGED_VERSIONSEQUENCE);
                                if (row[col].ToString() == ADDED_VERSIONSEQUENCE)
                                    v = int.Parse(ADDED_VERSIONSEQUENCE);
                                cmd.Parameters.AddWithValue("@" + col.ColumnName, v);
                            }
                            else
                                cmd.Parameters.AddWithValue("@" + col.ColumnName, row[col]);
                        }

                        foreach (DataColumn c in pc)
                        {
                            cmd.Parameters.AddWithValue("@" + c.ColumnName, row[c]);
                        }

                        result = cmd.ExecuteNonQuery();
                    }

                    commitTrans();
                }
                catch (Exception ex)
                {
                    string sErrMsg = "[" + this.Name + "." + MethodBase.GetCurrentMethod().Name + "] " + ex.Message;
                    Console.WriteLine(sErrMsg);
                    result = 0;
                    rollbackTrans();
                    throw new ArgumentException(sErrMsg);
                }
            }
            return result;
        }

        public async Task<int> updateDataTableAsync(DataTable dt)
        {
            DataColumn[] pc = dt.PrimaryKey;
            if (pc.Length == 0)
                return -1;
            if (dt.Rows.Count == 0)
                return -1;
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("UPDATE {0} SET ", dt.TableName);
            bool bCheck = false;
            foreach (DataColumn c in dt.Columns)
            {
                if (pc.Contains(c) == true)
                    continue;

                if (bCheck)
                    sb.Append(" , ");

                //if (c.ColumnName.Equals("LAST_UPDATE") == true)
                //    sb.Append("LAST_UPDATE = datetime('now','+9 hour')");
                //else if (c.ColumnName.Equals("VERSION_SEQUENCE") == true)
                //    sb.Append("VERSION_SEQUENCE = " + CHANGED_VERSIONSEQUENCE);
                //else
                sb.AppendFormat(" {0} = @{0} ", c.ColumnName);
                bCheck = true;
            }
            sb.Append(" WHERE ( ");
            bCheck = false;
            foreach (DataColumn c in pc)
            {
                if (bCheck)
                    sb.Append(" AND ");
                sb.AppendFormat(" {0} = @{0} ", c.ColumnName);
            }
            sb.Append(" ) ");


            int result = 0;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                cmd.Prepare();

                try
                {
                    beginTrans();
                    foreach (DataRow row in dt.Rows)
                    {
                        cmd.Parameters.Clear();
                        foreach (DataColumn col in dt.Columns)
                        {
                            if (pc.Contains(col) == true)
                                continue;

                            if (col.ColumnName.Equals("LAST_UPDATE", StringComparison.CurrentCultureIgnoreCase) == true)
                            {
                                //if(row[col] == null)
                                cmd.Parameters.AddWithValue("@" + col.ColumnName, DateTime.Now);
                                //else
                                //    cmd.Parameters.AddWithValue("@" + col.ColumnName, row[col]);
                            }
                            else if (col.ColumnName.Equals("VERSION_SEQUENCE", StringComparison.CurrentCultureIgnoreCase) == true)
                            {
                                int v = int.Parse(CHANGED_VERSIONSEQUENCE);
                                if (row[col].ToString() == ADDED_VERSIONSEQUENCE)
                                    v = int.Parse(ADDED_VERSIONSEQUENCE);
                                //else
                                //    v = Convert.ToInt32(row[col]);
                                cmd.Parameters.AddWithValue("@" + col.ColumnName, v);
                            }
                            //continue; //cmd.Parameters.AddWithValue("@" + col.ColumnName, "datetime('now','+9 hour')");
                            else
                                cmd.Parameters.AddWithValue("@" + col.ColumnName, row[col]);
                        }

                        foreach (DataColumn c in pc)
                        {
                            cmd.Parameters.AddWithValue("@" + c.ColumnName, row[c]);
                        }
                        result += await cmd.ExecuteNonQueryAsync();
                    }
                    commitTrans();
                }
                catch (Exception ex)
                {
                    string sErrMsg = "[" + this.Name + "." + MethodBase.GetCurrentMethod().Name + "] " + ex.Message;
                    Console.WriteLine(sErrMsg);
                    result = 0;
                    rollbackTrans();
                    throw new ArgumentException(sErrMsg);
                }
            }
            return result;
        }

        public int insertDataTable(DataTable dt)
        {
            //bool bOutput = false;
            DataColumn[] pc = dt.PrimaryKey;
            if (pc.Length == 0)
                return -1;
            if (dt.Rows.Count == 0)
                return -1;
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("INSERT INTO {0} ", dt.TableName);

            bool bCheck = false;
            sb.Append(" ( ");
            foreach (DataColumn c in dt.Columns)
            {
                if (bCheck)
                    sb.Append(" , ");

                sb.AppendFormat("{0} ", c.ColumnName);
                bCheck = true;
            }
            sb.Append(" ) ");

            bCheck = false;
            sb.Append(" VALUES( ");
            foreach (DataColumn c in dt.Columns)
            {
                if (bCheck)
                    sb.Append(" , ");

                //if (c.ColumnName.Equals("LAST_UPDATE") == true)
                //    sb.Append("datetime('now','+9 hour')");
                //else if (c.ColumnName.Equals("VERSION_SEQUENCE") == true)
                //    sb.Append(CHANGED_VERSIONSEQUENCE);
                //else
                sb.Append("@" + c.ColumnName);
                bCheck = true;
            }
            sb.Append(" ) ");

            int result = 0;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                cmd.Prepare();
                beginTrans();

                string sql = "";
                try
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        cmd.Parameters.Clear();
                        foreach (DataColumn col in dt.Columns)
                        {
                            if (col.ColumnName.Equals("LAST_UPDATE", StringComparison.CurrentCultureIgnoreCase))
                            {
                                cmd.Parameters.AddWithValue("@" + col.ColumnName, DateTime.Now);
                            }
                            else if (col.ColumnName.Equals("VERSION_SEQUENCE", StringComparison.CurrentCultureIgnoreCase))
                            {
                                int v = int.Parse(ADDED_VERSIONSEQUENCE);
                                if (row[col].GetType() != typeof(DBNull) && row[col].ToString() != ADDED_VERSIONSEQUENCE)
                                    v = Convert.ToInt32(row[col]);

                                cmd.Parameters.AddWithValue("@" + col.ColumnName, v);
                            }
                            else
                                cmd.Parameters.AddWithValue("@" + col.ColumnName, row[col]);
                        }
                        sql = cmd.CommandText;
                        result += cmd.ExecuteNonQuery();
                    }
                    commitTrans();
                }
                catch (Exception ex)
                {
                    string sErrMsg = "[" + this.Name + "." + MethodBase.GetCurrentMethod().Name + "] " + ex.Message;
                    //Console.WriteLine(sErrMsg);

                    result = 0;
                    //rollbackTrans();
                    Console.WriteLine(cmd.CommandText);
                    foreach (SqliteParameter param in cmd.Parameters)
                    {
                        if (param.DbType == DbType.Binary)
                        {
                            byte[] b = (byte[])param.Value;
                            string temp = BitConverter.ToString(b);
                            Guid id = new Guid(temp.Replace("-", ""));
                            Console.WriteLine(param.ParameterName + " | " + id);
                        }
                        else
                        {
                            Console.WriteLine(param.ParameterName + " | " + param.Value.ToString());
                        }
                    }


                    throw new ArgumentException(sErrMsg);
                }
            }

            return result;
        }
        public async Task<int> insertDataTableAsync(DataTable dt)
        {
            DataColumn[] pc = dt.PrimaryKey;
            if (pc.Length == 0)
                return -1;
            if (dt.Rows.Count == 0)
                return -1;
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("INSERT INTO {0} ", dt.TableName);

            bool bCheck = false;
            sb.Append(" ( ");
            foreach (DataColumn c in dt.Columns)
            {
                if (bCheck)
                    sb.Append(" , ");

                sb.AppendFormat("{0} ", c.ColumnName);
                bCheck = true;
            }
            sb.Append(" ) ");
            bCheck = false;
            sb.Append(" VALUES( ");
            foreach (DataColumn c in dt.Columns)
            {
                if (bCheck)
                    sb.Append(" , ");
                //if (c.ColumnName.Equals("LAST_UPDATE") == true)
                //    sb.Append("datetime('now','+9 hour')");
                //else if (c.ColumnName.Equals("VERSION_SEQUENCE") == true)
                //    sb.Append(CHANGED_VERSIONSEQUENCE);
                //else
                sb.AppendFormat(" @{0} ", c.ColumnName);
                bCheck = true;
            }
            sb.Append(" ) ");
            //Console.Write(sb.ToString());
            int result = 0;

            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                cmd.Prepare();
                beginTrans();
                try
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        cmd.Parameters.Clear();
                        foreach (DataColumn col in dt.Columns)
                        {
                            if (col.ColumnName.Equals("LAST_UPDATE", StringComparison.CurrentCultureIgnoreCase) == true)
                            {
                                //if (row[col] == null)
                                cmd.Parameters.AddWithValue("@" + col.ColumnName, DateTime.Now);
                                //else
                                //    cmd.Parameters.AddWithValue("@" + col.ColumnName, row[col]);
                            }
                            else if (col.ColumnName.Equals("VERSION_SEQUENCE", StringComparison.CurrentCultureIgnoreCase) == true)
                            {
                                int v = int.Parse(ADDED_VERSIONSEQUENCE);
                                if (row[col].GetType() != typeof(DBNull) && row[col].ToString() != ADDED_VERSIONSEQUENCE)
                                    v = Convert.ToInt32(row[col]);
                                cmd.Parameters.AddWithValue("@" + col.ColumnName, v);
                            }
                            else
                                cmd.Parameters.AddWithValue("@" + col.ColumnName, row[col]);
                        }

                        result += await cmd.ExecuteNonQueryAsync();
                    }
                    commitTrans();
                }
                catch (Exception ex)
                {
                    string sErrMsg = "[" + this.Name + "." + MethodBase.GetCurrentMethod().Name + "] " + ex.Message;
                    Console.WriteLine(sErrMsg);
                    result = 0;
                    rollbackTrans();
                }
            }
            return result;
        }

        public int deleteDataTable(DataTable dt)
        {
            DataColumn[] pc = dt.PrimaryKey;
            if (pc.Length == 0)
                return -1;
            if (dt.Rows.Count == 0)
                return -1;
            StringBuilder sb = new StringBuilder();
            StringBuilder sb2 = new StringBuilder();
            sb.AppendFormat("DELETE FROM {0} ", dt.TableName);
            sb2.AppendFormat("UPDATE {0} SET VERSION_SEQUENCE=-7777 ", dt.TableName);

            bool bCheck = false;

            int deleteCount = 0;
            int updateCount = 0;
            sb.Append(" WHERE ( ");
            sb2.Append(" WHERE ( ");
            foreach (DataColumn c in pc)
            {
                if (bCheck)
                {
                    sb.Append(" AND ");
                    sb2.Append(" AND ");
                }
                sb.AppendFormat(" {0} = @{0} ", c.ColumnName);
                sb2.AppendFormat(" {0} = @{0} ", c.ColumnName);
                bCheck = true;
            }
            sb.Append(" ) ");
            sb2.Append(" ) ");

            int result = 0;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                cmd.Prepare();

                bool checkVersion = dt.Columns.Contains("VERSION_SEQUENCE");
                beginTrans();
                try
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        if (checkVersion)
                        {
                            int version = Convert.ToInt32(row["VERSION_SEQUENCE"]);
                            if (version.ToString() != ADDED_VERSIONSEQUENCE)
                            {
                                row["VERSION_SEQUENCE"] = DELETED_VERSIONSEQUENCE;
                                continue;
                            }
                        }
                        deleteCount++;
                        cmd.Parameters.Clear();
                        foreach (DataColumn c in pc)
                        {
                            cmd.Parameters.AddWithValue("@" + c.ColumnName, row[c]);
                            Guid ID = new Guid((byte[])row[c]);
                            string p = "@" + c.ColumnName + ":" + Common.Utils.GuidDBString(ID);
                            p = p.ToLower();
                        }
                        result += cmd.ExecuteNonQuery();
                    }

                    commitTrans();
                }
                catch (Exception ex)
                {
                    string sErrMsg = "[" + this.Name + "." + MethodBase.GetCurrentMethod().Name + "] " + ex.Message;
                    //Console.WriteLine(sErrMsg);
                    result = 0;
                    rollbackTrans();
                    throw new ArgumentException(sErrMsg);
                }

                if (checkVersion && result != dt.Rows.Count) //업데이트 필요
                {
                    using (SqliteCommand cmd2 = new SqliteCommand(_sqliteConnection))
                    {
                        cmd2.CommandText = sb2.ToString();
                        cmd2.Prepare();

                        beginTrans();
                        try
                        {
                            foreach (DataRow row in dt.Rows)
                            {
                                if (checkVersion)
                                {
                                    int version = Convert.ToInt32(row["VERSION_SEQUENCE"]);
                                    if (version.ToString() == ADDED_VERSIONSEQUENCE)
                                        continue;
                                }
                                updateCount++;
                                cmd2.Parameters.Clear();
                                foreach (DataColumn c in pc)
                                {
                                    cmd2.Parameters.AddWithValue("@" + c.ColumnName, row[c]);
                                }
                                result += cmd2.ExecuteNonQuery();
                            }

                            commitTrans();
                        }
                        catch (Exception ex)
                        {
                            string sErrMsg = "[" + this.Name + "." + MethodBase.GetCurrentMethod().Name + "] " + ex.Message;
                            //Console.WriteLine(sErrMsg);
                            result = 0;
                            rollbackTrans();
                            throw new ArgumentException(sErrMsg);
                        }
                    }
                }
            }
            return result;
        }

        public async Task<int> deleteDataTableAsync(DataTable dt)
        {
            DataColumn[] pc = dt.PrimaryKey;
            if (pc.Length == 0)
                return -1;
            if (dt.Rows.Count == 0)
                return -1;
            StringBuilder sb = new StringBuilder();
            StringBuilder sb2 = new StringBuilder();

            sb.AppendFormat("DELETE FROM {0} ", dt.TableName);
            sb2.AppendFormat("UPDATE {0} SET VERSION_SEQUENCE=-7777 ", dt.TableName);

            bool bCheck = false;

            sb.Append(" WHERE ( ");
            sb2.Append(" WHERE ( ");
            foreach (DataColumn c in pc)
            {
                if (bCheck)
                {
                    sb.Append(" AND ");
                    sb2.Append(" AND ");
                }
                sb.AppendFormat(" {0} = @{0} ", c.ColumnName);
                sb2.AppendFormat(" {0} = @{0} ", c.ColumnName);
                bCheck = true;
            }
            sb.Append(" ) ");
            sb2.Append(" ) ");

            int result = 0;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                cmd.Prepare();
                bool checkVersion = dt.Columns.Contains("VERSION_SEQUENCE");
                beginTrans();
                try
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        if (checkVersion)
                        {
                            int version = Convert.ToInt32(row["VERSION_SEQUENCE"]);
                            if (version.ToString() != ADDED_VERSIONSEQUENCE)
                            {
                                row["VERSION_SEQUENCE"] = DELETED_VERSIONSEQUENCE;
                                continue;
                            }
                        }
                        cmd.Parameters.Clear();
                        foreach (DataColumn c in pc)
                        {
                            cmd.Parameters.AddWithValue("@" + c.ColumnName, row[c]);
                        }
                        result += await cmd.ExecuteNonQueryAsync();
                    }
                    commitTrans();
                }
                catch (Exception ex)
                {
                    string sErrMsg = "[" + this.Name + "." + MethodBase.GetCurrentMethod().Name + "] " + ex.Message;
                    Console.WriteLine(sErrMsg);
                    result = 0;
                    rollbackTrans();
                }

                if (checkVersion && result != dt.Rows.Count) //업데이트 필요
                {
                    SqliteCommand cmd2 = new SqliteCommand(_sqliteConnection);
                    cmd2.CommandText = sb2.ToString();
                    cmd2.Prepare();

                    beginTrans();
                    try
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            if (checkVersion)
                            {
                                int version = Convert.ToInt32(row["VERSION_SEQUENCE"]);
                                if (version.ToString() == ADDED_VERSIONSEQUENCE)
                                    continue;
                            }
                            cmd2.Parameters.Clear();
                            foreach (DataColumn c in pc)
                            {
                                cmd2.Parameters.AddWithValue("@" + c.ColumnName, row[c]);
                            }
                            result += await cmd2.ExecuteNonQueryAsync();
                        }

                        commitTrans();
                    }
                    catch (Exception ex)
                    {
                        string sErrMsg = "[" + this.Name + "." + MethodBase.GetCurrentMethod().Name + "] " + ex.Message;
                        //Console.WriteLine(sErrMsg);
                        result = 0;
                        rollbackTrans();
                        throw new ArgumentException(sErrMsg);
                    }
                }
            }
            return result;
        }

        public void refresh()
        {
            throw new NotImplementedException();
        }
        public string getGuidString(Guid guid)
        {
            return "X'" + Common.Utils.GuidDBString(guid) + "'";
        }
        public string getGuidString(Guid[] guidList, string delimiter)
        {
            if (guidList == null)
                return "";

            StringBuilder sb = new StringBuilder();
            sb.Append(getGuidString(guidList[0]));
            for (int i = 1; i < guidList.Length; i++)
            {
                sb.Append(delimiter);
                sb.Append(getGuidString(guidList[i]));
            }
            return sb.ToString();
        }
    }

    /// <summary>
    ///  Server Update 전용
    /// </summary>
    public partial class SqliteConnection
    {
        public void dropIndex()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(@"DROP INDEX IF EXISTS AttrIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS AttrVersionIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS BoxInstanceIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS HoleInstanceIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS IDX_TB_PRODUCTION_DRAWING_GROUPS_ID;");
                sb.AppendLine(@"DROP INDEX IF EXISTS IDX_TB_PRODUCTION_DRAWING_GROUPS_NM;");
                sb.AppendLine(@"DROP INDEX IF EXISTS IDX_TB_PRODUCTION_DRAWING_GROUPS_VERSION_SEQUENCE;");
                sb.AppendLine(@"DROP INDEX IF EXISTS IDX_TB_PRODUCTION_DRAWING_GROUP_ID;");
                sb.AppendLine(@"DROP INDEX IF EXISTS IDX_TB_PRODUCTION_DRAWING_ID;");
                sb.AppendLine(@"DROP INDEX IF EXISTS IDX_TB_PRODUCTION_DRAWING_VERSION;");
                sb.AppendLine(@"DROP INDEX IF EXISTS IndexmapMeberIndexmapIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS InstanceAABBsBuildingIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS InstanceAABBsIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS InstanceAabbVersionIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS InstanceBuildingIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS InstanceGroupMemberGroupIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS InstanceGroupMemberInstanceIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS InstanceGroupMemberVersionIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS ModelCategoryIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS ModelInstanceIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS ModelInstancePerentInstanceIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS ModelInstanceVersionIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS PipeInstanceIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS PipeInstanceVersionIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS PipeModuleIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS PocInstanceConnectedIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS PocInstanceIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS PocInstanceOrderIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS PocInstanceOwnerIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS PocInstanceVersionIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS PocModuleIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS StickerInstanceIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS StickerInstanceOwnerIndex;");
                sb.AppendLine(@"DROP INDEX IF EXISTS TB_MODELTEMPLATES_MODEL_TEMPLATE_NM_IDX;");

                beginTrans();
                using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
                {
                    cmd.CommandText = sb.ToString();
                    cmd.ExecuteNonQuery();
                }
                commitTrans();
            }
            catch (Exception ex)
            {
                rollbackTrans();
                throw ex;
            }
        }

        public void createIndex()
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                // TB_ATTRIBUTES
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS AttrIndex ON TB_ATTRIBUTES ( OWNER_ID );");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS AttrVersionIndex ON TB_ATTRIBUTES(VERSION_SEQUENCE);");

                // TB_BOXINSTANCES
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS BoxInstanceIndex ON TB_BOXINSTANCES (INSTANCE_ID);");

                // TB_HOLEINSTANCES
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS HoleInstanceIndex ON TB_HOLEINSTANCES (INSTANCE_ID);");

                // TB_PRODUCTION_DRAWING_GROUPS
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS IDX_TB_PRODUCTION_DRAWING_GROUPS_ID ON TB_PRODUCTION_DRAWING_GROUPS (PRODUCTION_DRAWING_GROUP_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS IDX_TB_PRODUCTION_DRAWING_GROUPS_NM ON TB_PRODUCTION_DRAWING_GROUPS (PRODUCTION_DRAWING_GROUP_NM);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS IDX_TB_PRODUCTION_DRAWING_GROUPS_VERSION_SEQUENCE ON TB_PRODUCTION_DRAWING_GROUPS (VERSION_SEQUENCE);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS IDX_TB_PRODUCTION_DRAWING_ID ON TB_PRODUCTION_DRAWING (PRODUCTION_DRAWING_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS IDX_TB_PRODUCTION_DRAWING_GROUP_ID ON TB_PRODUCTION_DRAWING (PRODUCTION_DRAWING_GROUP_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS IDX_TB_PRODUCTION_DRAWING_VERSION ON TB_PRODUCTION_DRAWING (VERSION_SEQUENCE);");

                // TB_INDEXMAPMEMBERS
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS IndexmapMeberIndexmapIndex ON TB_INDEXMAPMEMBERS (INDEXMAP_ID);");

                // TB_INSTANCEAABBS
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS InstanceAABBsBuildingIndex ON TB_INSTANCEAABBS(BUILDING_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS InstanceAABBsIndex ON TB_INSTANCEAABBS (INSTANCE_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS InstanceAABBVersionIndex ON TB_INSTANCEAABBS (VERSION_SEQUENCE);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS InstanceBuildingIndex ON TB_INSTANCEAABBS (BUILDING_ID);");

                // TB_INSTANCEGROUPMEMBERS
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS InstanceGroupMemberGroupIndex ON TB_INSTANCEGROUPMEMBERS (INSTANCE_GROUP_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS InstanceGroupMemberInstanceIndex ON TB_INSTANCEGROUPMEMBERS (INSTANCE_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS InstanceGroupMemberVersionIndex ON TB_INSTANCEGROUPMEMBERS(VERSION_SEQUENCE DESC);");

                // TB_MODELCATEGORIES
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS ModelCategoryIndex ON TB_MODELCATEGORIES (CATEGORY_ID);");

                // TB_MODELINSTANCES
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS ModelInstanceIndex ON TB_MODELINSTANCES (INSTANCE_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS ModelInstancePerentInstanceIndex ON TB_MODELINSTANCES (PARENT_INSTANCE_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS ModelInstanceVersionIndex ON TB_MODELINSTANCES (VERSION_SEQUENCE);");

                // TB_PIPEINSTANCES
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS PipeInstanceIndex ON TB_PIPEINSTANCES (INSTANCE_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS PipeInstanceVersionIndex ON TB_PIPEINSTANCES (VERSION_SEQUENCE);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS PipeModuleIndex ON TB_PIPEMODULEITEMS (MODULE_ID); ");

                // TB_POCINSTANCES
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS PocInstanceConnectedIndex ON TB_POCINSTANCES (CONNECTED_POC_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS PocInstanceIndex ON TB_POCINSTANCES (INSTANCE_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS PocInstanceOrderIndex ON TB_POCINSTANCES (OWNER_INSTANCE_ID, CONNECTION_ORDER); ");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS PocInstanceOwnerIndex ON TB_POCINSTANCES (OWNER_INSTANCE_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS PocInstanceVersionIndex ON TB_POCINSTANCES (VERSION_SEQUENCE);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS PocModuleIndex ON TB_POCMODULEITEMS (MODULE_ID);");

                // TB_STICKERINSTANCES
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS StickerInstanceIndex ON TB_STICKERINSTANCES (INSTANCE_ID);");
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS StickerInstanceOwnerIndex ON TB_STICKERINSTANCES (OWNER_INSTANCE_ID);");

                // TB_MODELTEMPLATES
                sb.AppendLine(@"CREATE INDEX IF NOT EXISTS TB_MODELTEMPLATES_MODEL_TEMPLATE_NM_IDX ON TB_MODELTEMPLATES(MODEL_TEMPLATE_NM);");

                beginTrans();
                using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
                {
                    cmd.CommandText = sb.ToString();
                    cmd.ExecuteNonQuery();
                }
                commitTrans();
            }
            catch (Exception ex)
            {
                rollbackTrans();
                throw ex;
            }
        }
        public int insert(string tableName, object[] rows)
        {
            if (rows == null || rows.Length == 0)
                return 0;

            int result = 0;

            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO ");
            sb.Append(tableName);

            sb.Append(" VALUES ");
            sb.Append(" ( ");
            sb.Append(string.Join(",", rows.Select(x => "?")));
            sb.Append(" ) ");

            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                for (int i = 0; i < rows.Length; i++)
                {
                    cmd.Parameters.Add(cmd.CreateParameter());
                };

                beginTrans();
                cmd.Prepare();
                try
                {
                    for (int i = 0; i < rows.Length; i++)
                    {
                        cmd.Parameters[i].Value = rows[i];
                    }

                    result += cmd.ExecuteNonQuery();
                    commitTrans();
                }
                catch (Exception ex)
                {
                    result = -1;
                    rollbackTrans();
                    throw ex;
                }
            }
            return result;
        }

        public int insert(string tableName, List<object[]> rows)
        {
            if (rows == null || rows.Count == 0)
                return 0;

            int result = 0;

            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO ");
            sb.Append(tableName);

            sb.Append(" VALUES ");
            sb.Append(" ( ");
            sb.Append(string.Join(",", rows[0].Select(x => "?")));
            sb.Append(" ) ");

            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                for (int i = 0; i < rows[0].Length; i++)
                {
                    cmd.Parameters.Add(cmd.CreateParameter());
                };

                beginTrans();
                cmd.Prepare();
                try
                {
                    foreach (object[] row in rows)
                    {
                        for (int i = 0; i < row.Length; i++)
                        {
                            cmd.Parameters[i].Value = row[i];
                        }

                        result += cmd.ExecuteNonQuery();
                    }
                    commitTrans();
                }
                catch (Exception ex)
                {
                    result = -1;
                    rollbackTrans();
                    throw;
                }
            }
            return result;
        }

        public int insert(string tableName, string[] columns, List<object[]> rows)
        {
            int result = 0;

            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO ");
            sb.Append(tableName);

            sb.Append(" ( ");
            sb.Append(string.Join(" , ", columns));
            sb.Append(" ) ");

            sb.Append(" VALUES ");
            sb.Append(" ( ");
            sb.Append(string.Join(",", columns.Select(x => "?")));
            sb.Append(" ) ");

            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                for (int i = 0; i < columns.Length; i++)
                {
                    cmd.Parameters.Add(cmd.CreateParameter());
                };

                beginTrans();
                cmd.Prepare();
                try
                {
                    foreach (object[] row in rows)
                    {
                        for (int i = 0; i < columns.Length; i++)
                        {
                            cmd.Parameters[i].Value = row[i];
                        }

                        result += cmd.ExecuteNonQuery();
                    }
                    commitTrans();
                }
                catch (Exception ex)
                {
                    result = -1;
                    rollbackTrans();
                    throw ex;
                }
            }
            return result;
        }

        public int delete(string tableName, string columnID, List<Guid> idList)
        {
            int result = 0;

            StringBuilder sb = new StringBuilder();
            sb.Append("DELETE FROM ");
            sb.Append(tableName);
            sb.Append(" WHERE ");
            sb.Append(columnID);
            sb.Append(" = @ID ");

            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                cmd.Prepare();
                beginTrans();
                try
                {
                    foreach (Guid id in idList)
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("ID", id.ToByteArray());

                        result += cmd.ExecuteNonQuery();
                    }
                    commitTrans();
                }
                catch (Exception ex)
                {
                    result = -1;
                    rollbackTrans();
                    throw ex;
                }
            }
            return result;
        }

        public object[] select(string tableName, string whereColumn, object value)
        {
            StringBuilder sql = new StringBuilder();
            sql.AppendFormat("SELECT * FROM {0}", TABLE_NAMES.MODEL_TEMPLATE);
            sql.AppendFormat(" WHERE MODEL_TEMPLATE_NM = @value");

            object[] values = null;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sql.ToString();
                cmd.Parameters.AddWithValue("@value", value);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        values = new object[reader.FieldCount];
                        reader.GetValues(values);
                    }
                }
            }

            return values;
        }

        public int insert(DataTable dt)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("INSERT INTO {0} ", dt.TableName);

            List<string> columns = new List<string>();
            foreach (DataColumn c in dt.Columns)
            {
                columns.Add(c.ColumnName);
            }
            sb.AppendFormat(" ( {0} )", string.Join(",", columns));
            sb.AppendFormat(" VALUES ( {0} )", string.Join(",", columns.Select(x => "@" + x)));

            int result = 0;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                cmd.Prepare();
                beginTrans();

                string sql = "";
                try
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        cmd.Parameters.Clear();
                        foreach (DataColumn col in dt.Columns)
                        {
                            cmd.Parameters.AddWithValue("@" + col.ColumnName, row[col]);
                        }
                        sql = cmd.CommandText;
                        result += cmd.ExecuteNonQuery();
                    }
                    commitTrans();
                }
                catch (Exception ex)
                {
                    string sErrMsg = "[" + this.Name + "." + MethodBase.GetCurrentMethod().Name + "] " + ex.Message;

                    throw new ArgumentException(sErrMsg);
                }
            }

            return result;
        }

        public int update(DataTable dt)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("UPDATE {0} SET ", dt.TableName);

            List<string> columns = new List<string>();
            foreach (DataColumn c in dt.Columns)
            {
                columns.Add(string.Format("{0} = @{0}", c.ColumnName));
            }
            sb.AppendFormat("{0}", string.Join(" , ", columns));

            DataColumn[] pc = dt.PrimaryKey;
            sb.AppendFormat(" WHERE {0} = @{0}", pc[0].ColumnName);

            int result = 0;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                cmd.Prepare();

                try
                {
                    beginTrans();
                    foreach (DataRow row in dt.Rows)
                    {
                        cmd.Parameters.Clear();
                        foreach (DataColumn col in dt.Columns)
                        {
                            cmd.Parameters.AddWithValue("@" + col.ColumnName, row[col]);
                        }

                        foreach (DataColumn c in pc)
                        {
                            cmd.Parameters.AddWithValue("@" + c.ColumnName, row[c]);
                            break;
                        }

                        result += cmd.ExecuteNonQuery();
                    }
                    commitTrans();
                }
                catch (Exception ex)
                {
                    string sErrMsg = "[" + this.Name + "." + MethodBase.GetCurrentMethod().Name + "] " + ex.Message;

                    throw new ArgumentException(sErrMsg);
                }
            }
            return result;
        }

        public int delete(DataTable dt)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("DELETE FROM {0} ", dt.TableName);

            DataColumn[] pc = dt.PrimaryKey;
            sb.AppendFormat(" WHERE {0} = @{0}", pc[0].ColumnName);

            int result = 0;
            using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
            {
                cmd.CommandText = sb.ToString();
                cmd.Prepare();

                try
                {
                    beginTrans();
                    foreach (DataRow row in dt.Rows)
                    {
                        cmd.Parameters.Clear();

                        foreach (DataColumn c in pc)
                        {
                            cmd.Parameters.AddWithValue("@" + c.ColumnName, row[c]);
                            break;
                        }

                        result += cmd.ExecuteNonQuery();
                    }
                    commitTrans();
                }
                catch (Exception ex)
                {
                    string sErrMsg = "[" + this.Name + "." + MethodBase.GetCurrentMethod().Name + "] " + ex.Message;

                    throw new ArgumentException(sErrMsg);
                }
            }
            return result;
        }

        public DataTable GetSchema(string collectionName)
        {
            string strSQL = string.Format("SELECT * FROM {0} where 1=0", collectionName);
            using (DbDataReader reader = executeReader(strSQL))
            {
                return reader.GetSchemaTable();
            };
        }

        public int removeDeletedInstance()
        {
            int result = 0;

            try
            {
                beginTrans();
                foreach (string tableName in TABLE_NAMES.S_INSTANCE_TABLE_NAMES)
                {
                    string query = string.Format("DELETE FROM {0} WHERE VERSION_SEQUENCE=-7777", tableName);
                    using (SqliteCommand cmd = new SqliteCommand(_sqliteConnection))
                    {
                        cmd.CommandText = query;
                        result += Convert.ToInt32(cmd.ExecuteNonQuery());
                    }
                }
                commitTrans();
            }
            catch (Exception ex)
            {
                result = -1;
                rollbackTrans();
                throw ex;
            }
            return result;
        }

        public void compression()
        {
            using (SqliteCommand cmd = new SqliteCommand("VACUUM", _sqliteConnection))
                cmd.ExecuteNonQuery();
        }
    }
}

