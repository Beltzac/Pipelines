using CSharpDiff.Patches.Models;
using Oracle.ManagedDataAccess.Client;

namespace Common
{
    public class OracleSchemaService
    {
        string devConnectionString = "User Id=AHOY_ABELTZAC;Password=!Hunt93cey111;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=racscandr.tcp.com.br)(PORT=1521))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=navisstby)));";
        string qaConnectionString = "User Id=TCPAPI;Password=TCPAPI;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=srvoracledbdev01.tcp.com.br)(PORT=1521))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME=navisqa)));";
        string schema = "TCPAPI";

        public Dictionary<string, string> Compare()
        {
            var devViews = GetViewDefinitions(devConnectionString, schema);
            var qaViews = GetViewDefinitions(qaConnectionString, schema);

            return CompareViewDefinitions(devViews, qaViews);
        }

        public Dictionary<string, string> GetViewDefinitions(string connectionString, string schema)
        {
            var viewDefinitions = new Dictionary<string, string>();

            using (var connection = new OracleConnection(connectionString))
            {
                connection.Open();
                using (var command = new OracleCommand($"SELECT VIEW_NAME, TEXT FROM ALL_VIEWS WHERE OWNER = '{schema}'", connection))
                {
                    command.InitialLONGFetchSize = -1;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string viewName = reader.GetString(0);
                            string viewText = reader.GetOracleString(1).Value;
                            viewDefinitions[viewName] = viewText;
                        }
                    }
                }
            }

            return viewDefinitions;
        }

        public Dictionary<string, string> CompareViewDefinitions(Dictionary<string, string> devViews, Dictionary<string, string> qaViews)
        {
            Dictionary<string, PatchResult> difs = new Dictionary<string, PatchResult>();

            foreach (var viewName in devViews.Keys)
            {
                if (qaViews.ContainsKey(viewName))
                {
                    difs.Add(viewName, OracleDiffService.GetDiff(viewName, devViews[viewName], qaViews[viewName]));
                }
                else
                {
                    difs.Add(viewName, OracleDiffService.GetDiff(viewName, devViews[viewName], string.Empty));

                    Console.WriteLine($"View {viewName} is present in DEV but not in QA");
                }
            }

            foreach (var viewName in qaViews.Keys)
            {
                if (!devViews.ContainsKey(viewName))
                {
                    difs.Add(viewName, OracleDiffService.GetDiff(viewName, string.Empty, qaViews[viewName]));

                    Console.WriteLine($"View {viewName} is present in QA but not in DEV");
                }
            }

            Dictionary<string, string> difsString = new Dictionary<string, string>();

            foreach (var kv in difs)
            {
                if (kv.Value.Hunks.Any())
                {
                    Console.WriteLine($"Difference in view: {kv.Key}");
                    difsString.Add(kv.Key, OracleDiffService.Format(kv.Value));
                }
            }

            return difsString;
        }
    }
}
