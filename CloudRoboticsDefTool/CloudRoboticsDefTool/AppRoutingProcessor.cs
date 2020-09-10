using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;


namespace CloudRoboticsDefTool
{
    public class AppRoutingProcessor
    {
        private string sqlConnectionString;
        private ApplicationException ae = null;

        public AppRoutingProcessor(string sqlConnectionString)
        {
            this.sqlConnectionString = sqlConnectionString;
        }

        public List<AppRoutingEntity> GetAppRoutings(string appId)
        {
            appId = appId.Replace("*", "%");

            string sqltext = "SELECT AppId,AppProcessingId,HttpUri,"
                           + "Status,Description,Registered_DateTime "
                           + "FROM RBFX.AppRouting2 "
                           + "WHERE AppId LIKE @p1 ORDER BY AppId,AppProcessingId";

            List<AppRoutingEntity> listOfAppRoutings = new List<AppRoutingEntity>();

            SqlConnection conn = new SqlConnection(this.sqlConnectionString);
            try
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(sqltext, conn);
                AddSqlParameter(ref cmd, "@p1", SqlDbType.NVarChar, appId);
                SqlDataReader reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    ae = new ApplicationException("No record was found in RBFX.AppRouting2");
                    throw ae;
                }

                while (reader.Read())
                {
                    AppRoutingEntity appRoutingEntity = new AppRoutingEntity();
                    appRoutingEntity.AppId = reader.GetString(0);
                    appRoutingEntity.AppProcessingId = reader.GetString(1);
                    if (!reader.IsDBNull(2))
                    {
                        appRoutingEntity.HttpUri = reader.GetString(2);
                    }
                    else
                    {
                        appRoutingEntity.HttpUri = string.Empty;
                    }

                    appRoutingEntity.Status = reader.GetString(3);

                    if (!reader.IsDBNull(4))
                    {
                        appRoutingEntity.Description = reader.GetString(4);
                    }
                    else
                    {
                        appRoutingEntity.Description = string.Empty;
                    }

                    if (!reader.IsDBNull(5))
                        appRoutingEntity.Registered_DateTime = reader.GetDateTime(5);

                    listOfAppRoutings.Add(appRoutingEntity);
                }

                reader.Close();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (ex.GetType().Equals(ae))
                    conn.Close();
                throw (ex);
            }

            return listOfAppRoutings;

        }

        public void insertAppRouting(AppRoutingEntity appRoutingEntity)
        {
            string sqltext = "INSERT INTO RBFX.AppRouting2 ("
                + "AppId,AppProcessingId,HttpUri,"
                + "Status,Description,Registered_DateTime"
                + ") VALUES ("
                + "@p1,@p2,@p3,@p4,@p5,@p6)";

            SqlConnection conn = new SqlConnection(this.sqlConnectionString);
            try
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(sqltext, conn);
                AddSqlParameter(ref cmd, "@p1", SqlDbType.NVarChar, appRoutingEntity.AppId);
                AddSqlParameter(ref cmd, "@p2", SqlDbType.NVarChar, appRoutingEntity.AppProcessingId);
                AddSqlParameter(ref cmd, "@p3", SqlDbType.NVarChar, appRoutingEntity.HttpUri);
                AddSqlParameter(ref cmd, "@p4", SqlDbType.NVarChar, appRoutingEntity.Status);
                AddSqlParameter(ref cmd, "@p5", SqlDbType.NVarChar, appRoutingEntity.Description);
                AddSqlParameter(ref cmd, "@p6", SqlDbType.DateTime, appRoutingEntity.Registered_DateTime);
                cmd.ExecuteNonQuery();

                conn.Close();
            }
            catch (Exception ex)
            {
                conn.Close();
                throw ex;
            }
        }

        public void updateAppRouting(AppRoutingEntity appRoutingEntity)
        {
            string sqltext = "UPDATE RBFX.AppRouting2 SET "
                + "HttpUri = @p3,"
                + "Status = @p4,"
                + "Description = @p5,"
                + "Registered_DateTime = @p6 "
                + "WHERE AppId = @p1 AND AppProcessingId = @p2";

            SqlConnection conn = new SqlConnection(this.sqlConnectionString);
            try
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(sqltext, conn);
                AddSqlParameter(ref cmd, "@p1", SqlDbType.NVarChar, appRoutingEntity.AppId);
                AddSqlParameter(ref cmd, "@p2", SqlDbType.NVarChar, appRoutingEntity.AppProcessingId);
                AddSqlParameter(ref cmd, "@p3", SqlDbType.NVarChar, appRoutingEntity.HttpUri);
                AddSqlParameter(ref cmd, "@p4", SqlDbType.NVarChar, appRoutingEntity.Status);
                AddSqlParameter(ref cmd, "@p5", SqlDbType.NVarChar, appRoutingEntity.Description);
                AddSqlParameter(ref cmd, "@p6", SqlDbType.DateTime, appRoutingEntity.Registered_DateTime);
                cmd.ExecuteNonQuery();

                conn.Close();
            }
            catch (Exception ex)
            {
                conn.Close();
                throw ex;
            }
        }

        public void deleteAppRouting(string appId, string appProcessingId)
        {
            string sqltext = "DELETE FROM RBFX.AppRouting2 WHERE AppId = @p1 AND AppProcessingId = @p2";

            SqlConnection conn = new SqlConnection(this.sqlConnectionString);
            try
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(sqltext, conn);
                AddSqlParameter(ref cmd, "@p1", SqlDbType.NVarChar, appId);
                AddSqlParameter(ref cmd, "@p2", SqlDbType.NVarChar, appProcessingId);
                cmd.ExecuteNonQuery();

                conn.Close();
            }
            catch (Exception ex)
            {
                conn.Close();
                throw ex;
            }
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
