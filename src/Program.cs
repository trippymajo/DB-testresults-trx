using Npgsql;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace TestResultDB
{
    internal class Program
    {
        private static void Main(string[] args)
        {

#if DEBUG
            string path = "C:\\res\\test02.trx";
            string timestamp = "2025_01_08_1627";
            string branch = "nano165";
            string ver = "0000000007";
#else
            if (args.Length < 1)
            {
                Console.WriteLine("Insufficient arguments provided. Exiting...");
                return;
            }

            string path = args[0];          // path = "D:\\Test.trx";
            string timestamp = args[1];     // timestamp = "2024_03_17_1617";
            string branch = args[2];        // branch = "nano160"; Test_ver
            string ver = args[3];           // ver = "24.5.6717.4721.Test";
#endif

            try
            {
                Convert.ToInt32(branch.Substring(4));
            }
            catch
            {
                // Achtung! need to handle this in near future for AS versions and etc
                Console.WriteLine("Branch name unsupported to write in DB. Problem is postfix of the {0}", branch);
                return;
            }

            // fix time from PS $Global:Timestamp
            // переводим Timestamp из string в Datetime
            DateTime dateTime = DateTime.ParseExact(timestamp, "yyyy_MM_dd_HHmm", CultureInfo.InvariantCulture);

            if (!File.Exists(path))
            {
                Console.WriteLine($"The specified TRX file does not exist: {path}");
                return;
            }

            try
            {
                //Парсим результаты из TRX
                TestRun tr = TrxDeserializer.Deserialize(path);

                //Создаем подключение к ДБ
                DataBase db = new DataBase(branch, ver, timestamp);

                if (db != null)
                {
                    int branchId = ProcessBranchId(db, branch);
                    if (branchId < 0)
                    {
                        db.Dispose();
                        return;
                    }

                    // Adding new test run information in to the DB
                    int testRunId = db.InsertTestRun(branchId, ver, dateTime);

                    //Создаем словарь для чтения из БД (таблицы tests) пары testname - id
                    Dictionary<string, int> map = db.GetTests();

                    //Если тест из результатов есть в БД, то добавляем результат в БД в таблицу Results по id из словаря
                    //Если нет, то сначала добавляем тест в БД (таблица tests) и получаем его id в БД.
                    //Потом добавляем результат в БД в таблицу Results по полученному id
                    var results = tr.Results.UnitTestResults.GroupBy(x => x.TestName).ToList();

                    foreach (var val in results)
                    {
                        var firstVal = val.FirstOrDefault();
                        if (firstVal != null)
                        {
                            string testResult = firstVal.Outcome;
                            string testName = firstVal.TestName;

                            if (map.ContainsKey(testName))
                            {
                                db.InsertResult(testRunId, map[testName], testResult);
                            }
                            else
                            {
                                int testId = db.InsertTest(testName);
                                db.InsertResult(testRunId, testId, testResult);
                            }
                        }
                        else
                        {
                            Console.WriteLine("No results provided from .trx file");
                            db.Dispose();
                            return;
                        }
                    }
                }

                db.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"A critical error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves int number from branch name
        /// </summary>
        /// <param name="branch">Branch's name</param>
        /// <returns>Int value of the branch name, ignoring postfixes and prefixes</returns>
        private static int MakeBranchInt(string branch)
        {
            string numberString = Regex.Replace(branch, @"\D", "");
            return Convert.ToInt32(numberString);
        }

        /// <summary>
        /// Processes branch workaround with DB. Returning the id of the branch current branch.
        /// </summary>
        /// <param name="branch"></param>
        /// <returns>
        /// [-1] - No need to process further DB operation. Branch is inactive or older than current actives.
        /// </returns>
        private static int ProcessBranchId(DataBase db, string branch)
        {
            int retVal = db.GetBranchId(branch);

            if (retVal > 0) // Process existing branch
            {
                // If branch is inactive, then don't care about storing results.
                if (!db.IsActiveBranch(branch))
                {
                    Console.WriteLine("Branch {0} is not active, no need to write results.", branch);
                    return -1;
                }
            }
            else // Insert new branch, make old one inactive
            {
                // Get the most oldest branch
                var minActiveBranch =db.GetMinActiveBranch();
                if (minActiveBranch != null)
                {
                    if (MakeBranchInt(minActiveBranch.Value.branch) > MakeBranchInt(branch))
                        return -1;
                    else
                    {
                        db.ArchiveBranch(minActiveBranch.Value.branch);
                    }
                }
                retVal = db.InsertBranch(branch); // insert new branch
            }

            return retVal;
        }
    }
}