using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using System.Data;
using Mono.Data.Sqlite;

/// <summary>
/// 对数据库文件路径的封装
/// </summary>
public class DbPath
{
    // 数据库存放在Assets/StreamingAssets/MyDatabase.db
    private static string MyDatabaseFileName = "MyDatabase1.db";

    private static string myDatabaseFilePath = string.Empty;
    public static string MyDatabaseFilePath
    {
        get
        {
            if (string.IsNullOrEmpty(myDatabaseFilePath))
            {
                myDatabaseFilePath = Application.streamingAssetsPath + Path.AltDirectorySeparatorChar + MyDatabaseFileName;
            }
            return myDatabaseFilePath;
        }
    }
}

public class User
{
    public string Id { get; set; }
    public int Age { get; set; }
    public string Name { get; set; }
    public float Height { get; set; }
}

public class DbManager : MonoBehaviour
{
    private IDbConnection connection;
    private IDbCommand command;
    private IDataReader reader;

    private void Awake()
    {
        CreateDbFile<User>();
    }

    private void Start()
    {
        OpenConnect();

        // Insert();
         GetAllData();
        //UpdateData();
        //Delete();
        // GetAllData();
        //DeleteTable();
    }

    private void OnDestroy()
    {
        CloseConnection();
    }

    #region 数据库操作
    /// <summary>
    /// 新建数据库连接、打开数据库
    /// </summary>
    public void OpenConnect()
    {
        try
        {
            // 新建数据库连接
            connection = new SqliteConnection(@"Data Source = " + DbPath.MyDatabaseFilePath);
            // 打开数据库
            connection.Open();
            Debug.Log("打开数据库成功...");
        }
        catch (Exception e)
        {
            Debug.LogError("打开数据库失败: " + GetExceptionInfo(e));
        }
    }

    /// <summary>
    /// 关闭数据库
    /// </summary>
    public void CloseConnection()
    {
        if (reader != null)
        {
            reader.Close();
            reader.Dispose();
            reader = null;
        }

        if (command != null)
        {
            command.Dispose();
            command = null;
        }
        if (connection != null)
        {
            connection.Close();
            connection.Dispose();
            connection = null;
        }
    }

    /// <summary>
    /// 执行Sql命令
    /// </summary>
    /// <param name="queryStr"></param>
    /// <returns></returns>
    public IDataReader ExecuteSqlCommand(string queryStr)
    {
        Monitor.Enter(queryStr);
        try
        {
            command = connection.CreateCommand();
            command.CommandText = queryStr;
            reader = command.ExecuteReader();
            Debug.Log("Execute queryStr = " + queryStr + " Success...");
            return reader;
        }
        catch (Exception e)
        {
            Debug.LogError(string.Format("sqlite ExecuteNonQuery query: {0} failed details {1}", queryStr, GetExceptionInfo(e)));
            return null;
        }
        finally
        {
            Monitor.Exit(queryStr);
        }
    }
    #endregion 数据库操作

    #region 创建数据库文件
    public void CreateDbFile<T>()
    {
        if (!File.Exists(DbPath.MyDatabaseFilePath))
        {
            // 如果数据库文件不存在, 下面这一步新建数据库连接和打开数据库之后会自动创建数据库文件
            OpenConnect();

            Type type = typeof(T);
            CreateTable(type.Name, type.GetProperties());

            CloseConnection();
        }
    }

    public IDataReader CreateTable(string tableName, PropertyInfo[] piArr)
    {
        string queryStr = "create table " + tableName + " (" + piArr[0].Name + " " + CS2DbType(piArr[0].PropertyType);
        for (int i = 1; i < piArr.Length; i++)
        {
            queryStr += ", " + piArr[i].Name + " " + CS2DbType(piArr[i].PropertyType);
        }
        queryStr += ")";

        return ExecuteSqlCommand(queryStr);
    }

    private string CS2DbType(Type type)
    {
        string result = "Text";
        if (type == typeof(int))
        {
            result = "Int";
        }
        else if (type == typeof(string))
        {
            result = "Text";
        }
        else if (type == typeof(float))
        {
            result = "FLOAT";
        }
        else if (type == typeof(bool))
        {
            result = "Bool";
        }

        return result;
    }
    #endregion 创建数据库文件

    #region 插入数据
    public void Insert()
    {
        for (int i = 1; i < 10; i++)
        {
            User user = new User();
            user.Id = "000" + i;
            user.Age = 20 + i;
            user.Name = "zhangsan";
            user.Height = 178.5f + i;

            Insert<User>(user);
        }
    }

    public IDataReader Insert<T>(T t)
    {
        Type type = typeof(T);
        string sql = "insert into " + type.Name + " values (";
        foreach (var pi in type.GetProperties())
        {
            sql += "'" + type.GetProperty(pi.Name).GetValue(t, null) + "',";
        }
        sql = sql.TrimEnd(',') + ");";
        return ExecuteSqlCommand(sql);
    }
    #endregion 插入数据

    #region 获取数据
    public void GetAllData()
    {
        List<User> list = GetAllData<User>();
        foreach (var user in list)
        {
            Debug.Log(string.Format("Id = {0} Name = {1} Age = {2} Height = {3}", user.Id, user.Name, user.Age, user.Height));
        }
    }

    public List<T> GetAllData<T>() where T : new()
    {
        List<T> list = new List<T>();

        Type type = typeof(T);
        string sql = "select * from " + type.Name;
        reader = ExecuteSqlCommand(sql);
        while (reader.Read())
        {
            T t = new T();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                // reader.GetValue(i)返回object类型不能通过反射进行赋值, 为什么不能???????
                // 因此这里进行了一次转换
                type.GetProperty(reader.GetName(i)).SetValue(t, Db2CSValue(type.GetProperty(reader.GetName(i)).PropertyType, reader.GetValue(i)), null);
            }
            list.Add(t);
        }

        return list;
    }

    private object Db2CSValue(Type type, object value)
    {
        object result = null;
        if (type == typeof(int))
        {
            result = Convert.ToInt32(value);
        }
        else if (type == typeof(string))
        {
            result = Convert.ToString(value);
        }
        else if (type == typeof(float))
        {
            result = Convert.ToSingle(value);
        }
        else if (type == typeof(bool))
        {
            result = Convert.ToBoolean(value);
        }
        return result;
    }
    #endregion 获取数据

    #region 更新数据
    public void UpdateData()
    {
        User user = new User();
        user.Id = "0001";
        user.Age = 16;
        user.Name = "xiaohua";
        user.Height = 168.2f;

        UpdateData<User>(user);
    }

    public IDataReader UpdateData<T>(T t)
    {
        object idValue = string.Empty;
        Type type = typeof(T);
        StringBuilder sb = new StringBuilder("update ");
        sb.Append(type.Name).Append(" set ");
        foreach (var pi in type.GetProperties())
        {
            if (pi.Name.Equals("Id"))
            {
                idValue = pi.GetValue(t, null);
            }
            sb.Append(pi.Name).Append("='").Append(pi.GetValue(t, null)).Append("',");
        }
        sb.Remove(sb.Length - 1, 1);
        sb.Append(" where (").Append("Id='").Append(idValue).Append("');");

        return ExecuteSqlCommand(sb.ToString());
    }
    #endregion 更新数据

    #region 删除数据
    public void Delete()
    {
        User user = new User();
        user.Id = "0001";

        Delete<User>(user);
    }

    public IDataReader Delete<T>(T t)
    {
        object idValue = string.Empty;
        Type type = typeof(T);
        foreach (var pi in type.GetProperties())
        {
            if (pi.Name.Equals("Id"))
            {
                idValue = pi.GetValue(t, null);
            }
        }
        string sql = "delete from " + type.Name + " where (Id='" + idValue + "');";

        return ExecuteSqlCommand(sql);
    }
    #endregion 删除数据

    #region 卸载表, 客户端一般不用哈, 这么畸形
    public void DeleteTable()
    {
        if (File.Exists(DbPath.MyDatabaseFilePath))
        {
            DeleteDable<User>();
        }
    }

    public IDataReader DeleteDable<T>()
    {
        Type type = typeof(T);
        string sql = "drop table " + type.Name;
        return ExecuteSqlCommand(sql);
    }
    #endregion 卸载表, 客户端一般不用哈, 这么畸形

    private string GetExceptionInfo(Exception e)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(e.Message);
        sb.AppendLine();
        sb.Append(e.StackTrace);
        return sb.ToString();
    }

}
