using project.data.types;

namespace project.application
{
    class LocalConnection
    {
        SessionFactory sF;

        private static string GetTableName(Type entityType)
        {
            var mappings = sF.GetClassMetadata(entityType) as NHibernate.Persister.Entity.AbstractEntityPersister;
            return mappings.TableName;
        }

        public static void Save<Entity>(Entity obj)
        {
            dynamic dObj = obj;
            var type = obj.GetType();
            using (var transaction = CurrentSession.BeginTransaction(IsolationLevel.ReadUncommitted))
            {
                try
                {
                    string tableName = type.Name.Contains("Proxy") ? GetTableName(type.BaseType) : GetTableName(type);
                    CurrentSession.CreateSQLQuery($"SET IDENTITY_INSERT {tableName} ON").ExecuteUpdate();
                    var exists = CurrentSession.CreateSQLQuery($"SELECT 1 FROM {tableName} where id = {dObj.Id}").UniqueResult<int>() == 1;
                    if (!exists)
                    {
                        CurrentSession.CreateSQLQuery($"DBCC CHECKIDENT ('{tableName}', RESEED, {dObj.Id - 1});").ExecuteUpdate();
                        CurrentSession.Save(obj, dObj.Id);
                    }
                    transaction.Commit();
                    CurrentSession.CreateSQLQuery($"SET IDENTITY_INSERT {tableName} OFF").ExecuteUpdate();
                }
                catch (Exception e)
                {
                    DisposeCurrentSession();
                    throw e;
                }
            }
        }
    }

    class Extract
    {
        #region Private
        private static List<dynamic> DynamicList = new List<dynamic>();
        private static List<Tuple<dynamic, dynamic>> HashSetLists = new List<Tuple<dynamic, dynamic>>();
        private static string CurrentNamespace = @"project\.data\.types";
        #endregion

        public static void From<T>(T obj, params Expression<Func<T, object>>[] skip)
        {
            if (obj == null)
                return;
            dynamic cObj = obj;
            string pattern = @$"{CurrentNamespace}\.([^\[\],]+?)(?=,)";
            int i = 0;
            int lastI = 0, totalCurrentNodes = 0;

            //Propiedades - Finalizado
            var nodeList = new Dictionary<object, (bool, bool)>();
            dynamic nodoPadre = null;
            dynamic nodoHijo = null;
            Type type = null;
        seguir:
            if (cObj == null)
                return;
            else if (!nodeList.ContainsKey(cObj)) { nodeList.Add(cObj, (false, false)); }

            type = cObj.GetType();

            if (!nodeList[cObj].Item1)
            {
                if (type.Name.ToLower().Contains("proxy"))
                    RemoteConnection.Session.Persist(cObj);

                var toInspectFull = type.GetProperties().Where(x =>
                    !x.PropertyType.Name.ToLower().Contains("object")
                    && x.PropertyType != typeof(System.String)
                    && ((
                    x.PropertyType.IsClass)
                    || (x.PropertyType.Name.ToLower().Contains("list")))
                ).ToList();

                if (skip != null && skip.Count() > 0)
                {
                    var currentExpressionsToSkip = skip.Where(y => ((MemberExpression)y.Body).Member.DeclaringType == type);
                    if (currentExpressionsToSkip.Count() > 0)
                    {
                        toInspectFull = toInspectFull.Where(x =>
                            !currentExpressionsToSkip.Any(y =>
                                   x.Name == ((MemberExpression)y.Body).Member.Name
                                && x.PropertyType == ((MemberExpression)y.Body).Type
                            )
                        ).ToList();
                    }
                }

                // Itera a través de las propiedades del objeto
                foreach (var property in toInspectFull)
                {
                    RemoteConnection.Session.Persist(cObj);
                    var propertyValue = property.GetValue(cObj);

                    if (propertyValue != null)
                    {
                        var propName = property.PropertyType.FullName;
                        var match = Regex.Match(propName, pattern);
                        var word = match.Groups[1].Value;
                        bool isList = propName.ToLower().Contains("ilist");

                        Type currentType = null;
                        if (isList)
                            currentType = propertyValue.GetType().GetGenericArguments()[0];
                        else
                            currentType = propertyValue.GetType();

                        dynamic list = Activator.CreateInstance(typeof(HashSetOnlyNotNull<>).MakeGenericType(currentType));

                        dynamic currentList = null;
                        foreach (var dL in DynamicList)
                        {
                            if (dL.GetType().GetGenericArguments()[0] == currentType)
                            {
                                currentList = dL;
                                break;
                            }
                        }

                        if (currentList == null)
                        {
                            DynamicList.Add(list);
                            currentList = list;
                        }

                        if (isList)
                        {
                            currentList.AddRange(propertyValue);
                        }
                        else
                        {
                            currentList.Add(propertyValue);
                        }
                    }
                }

                nodeList.Remove(cObj);
                nodeList.Add(cObj, (true, false));

                if (nodoPadre != null)
                {
                    lastI++;
                    var z = cObj;
                    cObj = nodoPadre;
                    nodoHijo = z;
                    goto seguir;
                }
            }
            else if (totalCurrentNodes > 0 && !nodeList[cObj].Item2 && lastI >= totalCurrentNodes)
            {
                nodeList.Remove(cObj);
                nodeList.Add(cObj, (true, true));
            }

            //Observar nodos
            var toInsepctOnlyObjects = type.GetProperties().Where(x =>
                   !x.PropertyType.Name.ToLower().Contains("object")
                && x.PropertyType != typeof(System.String)
                && x.PropertyType.IsClass
            ).ToList();

            if (skip != null && skip.Count() > 0)
            {
                var currentExpressionsToSkip = skip.Where(y => ((MemberExpression)y.Body).Member.DeclaringType == type);
                if (currentExpressionsToSkip.Count() > 0)
                {
                    toInsepctOnlyObjects = toInsepctOnlyObjects.Where(x =>
                        !currentExpressionsToSkip.Any(y =>
                               x.Name == ((MemberExpression)y.Body).Member.Name
                            && x.PropertyType == ((MemberExpression)y.Body).Type
                        )
                    ).ToList();
                }
            }

            totalCurrentNodes = toInsepctOnlyObjects.Count;
        seguirBuscando:
            if (lastI < totalCurrentNodes)
            {
                RemoteConnection.Session.Persist(cObj);
                nodoHijo = toInsepctOnlyObjects[lastI].GetValue(cObj);
                if (nodoHijo != null && !nodeList.ContainsKey(nodoHijo))
                {
                    nodoPadre = cObj;
                    cObj = nodoHijo;
                    nodoHijo = null;
                    goto seguir;
                }
                else if (lastI < totalCurrentNodes)
                {
                    lastI++;
                    if (lastI > totalCurrentNodes)
                    {
                        if (nodoPadre != null)
                        {
                            cObj = nodoPadre;
                            goto seguirBuscando;
                        }
                    }
                    goto seguirBuscando;
                }
                else
                {

                }
            }
            else
            {
                if (nodeList.ContainsKey(cObj) && nodeList[cObj].Item2)
                {
                    cObj = nodoHijo;
                    nodoPadre = null;
                    nodoHijo = null;
                    totalCurrentNodes = 0;
                    lastI = 0;
                    goto seguir;
                }
            }
        }


        public static void Main(string[] args)
        {
            //Get data from remote connection
            Person p;

            From<Person>(p, 
                x => x.Family,
                x => x.Friends,
            );
            RemoteConnection.Session.Close();

            foreach (var entity in DynamicList)
            {
                LocalConnection.Save(entity);
            }


        }
    }
}

namespace project.data.types
{
    public class Job
    {
        public virtual long Id { get; set; }
        public virtual string Name { get; set; }
    }

    public class Person
    {
        public virtual long Id { get; set; }
        public virtual ushort Age { get; set; }
        public IList<Job> Jobs { get; set; }
        public IList<Person> Family { get; set; }
        public IList<Person> Friends { get; set; }
    }


}

