using System;
using System.Data;
using System.Data.Common;
using System.Data.Objects;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace APFy.me.utilities
{
    public partial class APFyEntities
    {
        partial void OnContextCreated()
        {
            this.SavingChanges += new EventHandler(APFyEntities_SavingChanges);
        }

        private void APFyEntities_SavingChanges(object sender, EventArgs e)
        {
            var ctx = (ObjectContext)sender;

            //Check added and modified object
            foreach (ObjectStateEntry entry in
            ((ObjectContext)sender).ObjectStateManager.GetObjectStateEntries(
            EntityState.Added | EntityState.Modified | EntityState.Deleted))
            {
                //Logging modifications
                if (!entry.IsRelationship && (new Type[] { typeof(Method), typeof(RequestParameter), typeof(OutputValidation), typeof(OutputTransformation), typeof(Api), typeof(User), typeof(Page)}).Contains(entry.Entity.GetType()))
                {
                    Guid? userId = null;
                    if (System.Web.HttpContext.Current.User.Identity.IsAuthenticated)
                        userId = Guid.Parse(System.Web.HttpContext.Current.User.Identity.Name);

                    StringBuilder sb = new StringBuilder();
                    if (entry.State == EntityState.Modified) {
                        DbDataRecord original = entry.OriginalValues;

                        foreach (string propName in entry.GetModifiedProperties()) {
                            string oldValue = original.GetValue(
                                original.GetOrdinal(propName))
                                .ToString();

                            sb.AppendFormat("{0}={1}&", propName, System.Web.HttpUtility.UrlEncode(oldValue));
                        }

                        if(sb.Length > 0)
                            sb.Length = sb.Length - 1;
                    }
                    
                    Logger modLogger = LogManager.GetLogger("ModificationLog");
                    LogEventInfo modLog = new LogEventInfo(LogLevel.Info, "ModificationLog", sb.ToString());

                    modLog.Properties["TableName"] = entry.Entity.GetType().Name;
                    modLog.Properties["Id"] = entry.EntityKey.EntityKeyValues == null? 0 : (int)entry.EntityKey.EntityKeyValues[0].Value;
                    modLog.Properties["Operation"] = (int)entry.State;
                    modLog.Properties["UserId"] = userId;

                    modLogger.Log(modLog);

                    if (sb.Length > 0) {
                        Logger modChangelogger = LogManager.GetLogger("ModificationChangeLog");
                        modChangelogger.Log(modLog);
                    }
                }
            }
        }

        public IQueryable<Api> SearchApi(string q, Guid userId) {
            IQueryable<Api> query = this.Api.Where(a => a.Method.Any());

            if (!string.IsNullOrWhiteSpace(q))
            {
                Regex searchStringRe = new Regex(@"(?<prefix>(?:ns|m|alias|user)\:)?(?<value>(?:""[^""]+""|[^\s]+))");
                MatchCollection queryTokens = searchStringRe.Matches(q.Trim());

                string prefix;
                foreach (Match match in queryTokens)
                {
                    if (match.Groups["prefix"].Success)
                    {
                        prefix = match.Groups["prefix"].Value;
                        //if (!methodFilter.Keys.Contains(prefix))
                        //    methodFilter.Add(prefix, new List<string>());

                        //methodFilter[prefix].Add(match.Groups["value"].Value);
                    }
                    else
                        prefix = string.Empty;

                    string value = match.Groups["value"].Value;
                    if (prefix.Equals("ns:", System.StringComparison.OrdinalIgnoreCase))
                        query = query.Where(a => a.Method.Any(m => m.OutputValidation.Any(o => o.Namespace == value)));
                    else if (prefix.Equals("m:", System.StringComparison.OrdinalIgnoreCase))
                        query = query.Where(a => a.Method.Any(m => m.Verb.Name == value));
                    else if (prefix.Equals("alias:", System.StringComparison.OrdinalIgnoreCase))
                        query = query.Where(a => a.User.Any(u => u.Alias == value));
                    else if (prefix.Equals("user:", System.StringComparison.OrdinalIgnoreCase))
                        query = query.Where(a => a.User.Any(u => u.Guid == userId && u.Email == value));
                    else
                        query = query.Where(a => a.DomainBase.Contains(value));
                }
            }

            return query;
        }
    }
}
