using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;

namespace CloudRoboticsDefTool
{
    public class AppMasterProcessor
    {
        private string sqlConnectionString;
        private string encPassPhrase;
        private ApplicationException ae = null;

        public AppMasterProcessor(string sqlConnectionString, string encPassPhrase)
        {
            this.sqlConnectionString = sqlConnectionString;
            this.encPassPhrase = encPassPhrase;
        }

        public List<AppMasterEntity> GetAppMasters(string appId)
        {
            appId = appId.Replace("*", "%");

            string sqltext = "SELECT AppId,QueueStorageAccount,"
                           + "CONVERT(NVARCHAR(1000),DecryptByPassphrase(@passPhrase,QueueStorageKeyEnc,1,CONVERT(varbinary,AppId))) AS StorageKey,"
                           + "Status,Description,Registered_DateTime "
                           + "FROM RBFX.AppMaster2 "
                           + "WHERE AppId LIKE @p1 ORDER BY AppId";

            List<AppMasterEntity> listOfAppMasters = new List<AppMasterEntity>();

            SqlConnection conn = new SqlConnection(this.sqlConnectionString);
            try
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(sqltext, conn);
                AddSqlParameter(ref cmd, "@p1", SqlDbType.NVarChar, appId);
                AddSqlParameter(ref cmd, "@passPhrase", SqlDbType.NVarChar, encPassPhrase);
                SqlDataReader reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    ae = new ApplicationException("No record was found in RBFX.AppMaster2");
                    throw ae;
                }

                while (reader.Read())
                {
                    AppMasterEntity appMasterEntity = new AppMasterEntity();
                    appMasterEntity.AppId = reader.GetString(0);
                    appMasterEntity.QueueStorageAccount = reader.GetString(1);
                    if (!reader.IsDBNull(2))
                    {
                        appMasterEntity.QueueStorageKey = reader.GetString(2);
                    }
                    else
                    {
                        ae = new ApplicationException("Storage key can't be decrypted. Encryption Passphrase may be wrong.");
                        throw ae;
                    }

                    appMasterEntity.Status = reader.GetString(3);

                    if (!reader.IsDBNull(4))
                    {
                        appMasterEntity.Description = reader.GetString(4);
                    }
                    else
                    {
                        appMasterEntity.Description = string.Empty;
                    }

                    if (!reader.IsDBNull(5))
                        appMasterEntity.Registered_DateTime = reader.GetDateTime(5);

                    listOfAppMasters.Add(appMasterEntity);
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

            return listOfAppMasters;

        }

        public AppMasterEntity GetAppMaster(string appId)
        {
            AppMasterEntity appMasterEntity = new AppMasterEntity();

            string sqltext = "SELECT AppId,QueueStorageAccount,"
                           + "CONVERT(NVARCHAR(1000),DecryptByPassphrase(@passPhrase,QueueStorageKeyEnc,1,CONVERT(varbinary,AppId))) AS StorageKey,"
                           + "Status,Description,Registered_DateTime "
                           + "FROM RBFX.AppMaster2 "
                           + "WHERE AppId = @p1";

            SqlConnection conn = new SqlConnection(this.sqlConnectionString);
            try
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(sqltext, conn);
                AddSqlParameter(ref cmd, "@p1", SqlDbType.NVarChar, appId);
                AddSqlParameter(ref cmd, "@passPhrase", SqlDbType.NVarChar, encPassPhrase);
                SqlDataReader reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    ae = new ApplicationException("No record was found in RBFX.AppMaster2");
                    throw ae;
                }

                reader.Read();
                
                appMasterEntity.AppId = reader.GetString(0);
                appMasterEntity.QueueStorageAccount = reader.GetString(1);
                appMasterEntity.QueueStorageKey = reader.GetString(2);

                appMasterEntity.Status = reader.GetString(3);

                if (!reader.IsDBNull(4))
                {
                    appMasterEntity.Description = reader.GetString(4);
                }
                else
                {
                    appMasterEntity.Description = string.Empty;
                }

                if (!reader.IsDBNull(5))
                    appMasterEntity.Registered_DateTime = reader.GetDateTime(5);
                

                reader.Close();
                conn.Close();
            }
            catch (Exception ex)
            {
                if (ex.GetType().Equals(ae))
                    conn.Close();
                throw (ex);
            }

            return appMasterEntity;

        }

        public void insertAppMaster(AppMasterEntity appMasterEntity)
        {
            string sqltext = "INSERT INTO RBFX.AppMaster2 ("
                + "AppId,QueueStorageAccount,"
                + "QueueStorageKeyEnc,"
                + "Status,Description,Registered_DateTime"
                + ") VALUES ("
                + "@p1,@p2,"
                + "EncryptByPassPhrase(@passPhrase, @p3, 1, CONVERT(varbinary, @p1)),"
                + "@p4,@p5,@p6)";

            SqlConnection conn = new SqlConnection(this.sqlConnectionString);
            try
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(sqltext, conn);
                AddSqlParameter(ref cmd, "@p1", SqlDbType.NVarChar, appMasterEntity.AppId);
                AddSqlParameter(ref cmd, "@p2", SqlDbType.NVarChar, appMasterEntity.QueueStorageAccount);
                AddSqlParameter(ref cmd, "@p3", SqlDbType.NVarChar, appMasterEntity.QueueStorageKey);
                AddSqlParameter(ref cmd, "@p4", SqlDbType.NVarChar, appMasterEntity.Status);
                AddSqlParameter(ref cmd, "@p5", SqlDbType.NVarChar, appMasterEntity.Description);
                AddSqlParameter(ref cmd, "@p6", SqlDbType.DateTime, appMasterEntity.Registered_DateTime);
                AddSqlParameter(ref cmd, "@passPhrase", SqlDbType.NVarChar, encPassPhrase);
                cmd.ExecuteNonQuery();

                conn.Close();
            }
            catch (Exception ex)
            {
                conn.Close();
                throw ex;
            }
        }

        public void updateAppMaster(AppMasterEntity appMasterEntity)
        {
            string sqltext = "UPDATE RBFX.AppMaster2 SET "
                + "QueueStorageAccount = @p2,"
                + "QueueStorageKeyEnc = EncryptByPassPhrase(@passPhrase, @p3, 1, CONVERT(varbinary, @p1)),"
                + "Status = @p4,"
                + "Description = @p5,"
                + "Registered_DateTime = @p6 "
                + "WHERE AppId = @p1";

            SqlConnection conn = new SqlConnection(this.sqlConnectionString);
            try
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(sqltext, conn);
                AddSqlParameter(ref cmd, "@p1", SqlDbType.NVarChar, appMasterEntity.AppId);
                AddSqlParameter(ref cmd, "@p2", SqlDbType.NVarChar, appMasterEntity.QueueStorageAccount);
                AddSqlParameter(ref cmd, "@p3", SqlDbType.NVarChar, appMasterEntity.QueueStorageKey);
                AddSqlParameter(ref cmd, "@p4", SqlDbType.NVarChar, appMasterEntity.Status);
                AddSqlParameter(ref cmd, "@p5", SqlDbType.NVarChar, appMasterEntity.Description);
                AddSqlParameter(ref cmd, "@p6", SqlDbType.DateTime, appMasterEntity.Registered_DateTime);
                AddSqlParameter(ref cmd, "@passPhrase", SqlDbType.NVarChar, encPassPhrase);
                cmd.ExecuteNonQuery();

                conn.Close();
            }
            catch (Exception ex)
            {
                conn.Close();
                throw ex;
            }
        }

        public void deleteAppMaster(string appId)
        {
            string sqltext = "DELETE FROM RBFX.AppMaster2 WHERE AppId = @p1";

            SqlConnection conn = new SqlConnection(this.sqlConnectionString);
            try
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(sqltext, conn);
                AddSqlParameter(ref cmd, "@p1", SqlDbType.NVarChar, appId);
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
