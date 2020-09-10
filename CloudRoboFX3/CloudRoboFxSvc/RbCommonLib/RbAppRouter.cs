using System;
using System.Data.SqlClient;
using System.Data;

namespace CloudRoboticsCommon
{
    public class AppRouter
    {
        private ApplicationException ae;
        private string _appId = null;
        private string _appProcessingId = null;
        private string _sqlConnString = null;
        private int _expiredSecond = RbAppCacheType.DefaultCachingTimeSec;

        public AppRouter(string appId, string appProcessingId, string sqlConnString)
        {
            _appId = appId;
            _appProcessingId = appProcessingId;
            _sqlConnString = sqlConnString;
        }
        public AppRouter(string appId, string appProcessingId, string sqlConnString, int expiredSecond)
        {
            _appId = appId;
            _appProcessingId = appProcessingId;
            _sqlConnString = sqlConnString;
            _expiredSecond = expiredSecond;
        }
        public RbAppRouterCache GetAppRouting()
        {
            RbAppRouterCache apprc = new RbAppRouterCache();
            string sqltext = "SELECT AppId,AppProcessingId,HttpUri,[Status],"
                + "[Description],Registered_DateTime "
                + "FROM RBFX.AppRouting2 WHERE AppId = @p1 AND AppProcessingId = @p2 "
                + "AND Status = 'Active'";
            SqlConnection conn = new SqlConnection(_sqlConnString);
            try
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(sqltext, conn);
                AddSqlParameter(ref cmd, "@p1", SqlDbType.NVarChar, _appId);
                AddSqlParameter(ref cmd, "@p2", SqlDbType.NVarChar, _appProcessingId);
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    reader.Read();
                    apprc.AppId = reader.GetString(0);
                    apprc.AppProcessingId = reader.GetString(1);
                    apprc.HttpUri = reader.GetString(2);
                    apprc.Status = reader.GetString(3);
                    apprc.Description = reader.GetString(4);
                    apprc.Registered_DateTime = reader.GetDateTime(5);
                    TimeSpan ts = new TimeSpan(0, 0, _expiredSecond);
                    apprc.CacheExpiredDatetime = DateTime.Now + ts;
                    reader.Close();
                }
                else
                {
                    ae = new ApplicationException("Error ** No record found in RBFX.AppRouting");
                    throw (ae);
                }

                conn.Close();
            }
            catch (Exception ex)
            {
                if (ex.GetType().Equals(ae))
                    conn.Close();
                throw (ex);
            }
            return apprc;
        }
        private void AddSqlParameter(ref SqlCommand cmd, string ParameterName, SqlDbType type, Object value)
        {
            SqlParameter param = cmd.CreateParameter();
            param.ParameterName = ParameterName;
            param.SqlDbType = type;
            param.Direction = ParameterDirection.Input;
            param.Value = value;
            cmd.Parameters.Add(param);
        }

    }
}