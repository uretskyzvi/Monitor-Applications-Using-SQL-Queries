﻿using Microsoft.EnterpriseManagement.Common;
using Microsoft.EnterpriseManagement.Configuration;
using Microsoft.EnterpriseManagement.Mom.Internal.UI.Common;
using Microsoft.EnterpriseManagement.Mom.Internal.UI.Controls;
using Microsoft.EnterpriseManagement.Security;
using Microsoft.EnterpriseManagement.UI;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;

namespace ManageQueryOleDbMonitorUI
{
    public partial class ConnectionAndQuery : UIPage
    {
        #region Private Properties

        private string Database;
        private IList<EnterpriseManagementObject> Databases;
        private string principalName;
        private string templateIdString;
        private string UniqueID;
        private Dictionary<string, string> providers;
        private List<RunAsAccount> runAsAccountList;
        private string conStr;

        private string ProviderName
        {
            get
            {
                string text = cmbProvider.SelectedItem as string;
                if (text == null || !providers.ContainsKey(text))
                {
                    return null;
                }
                return providers[text];
            }
        }

        #endregion Private Properties

        #region Events

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            DBEngineChooserDialog dialog = new DBEngineChooserDialog(Container);
            dialog.ShowDialog(this);
            if (dialog.DialogResult == DialogResult.OK)
            {
                if (dialog.SelectedItems != null)
                {
                    ChooserControlItem selectedItem = dialog.SelectedItem as ChooserControlItem;
                    if ((selectedItem == null) || (selectedItem.Item == null))
                    {
                    }
                    else
                    {
                        EnterpriseManagementObject item = selectedItem.Item as EnterpriseManagementObject;
                        if (!string.IsNullOrEmpty(item.FullName))
                        {
                            if (item.DisplayName.ToLower() == "MSSQLServer".ToLower() || item.DisplayName.ToLower() == "SQLEXPRESS".ToLower())
                            {
                                txtInstanceName.Text = item.Path;
                            }
                            else
                            {
                                txtInstanceName.Text = item.Path + "\\" + item.DisplayName;
                            }

                            using (ManageQueryOleDBSDKHelper helper = new ManageQueryOleDBSDKHelper(ManagementGroup))
                            {
                                switch (item.GetClasses()[0].Name)
                                {
                                    case "Microsoft.SQLServer.DBEngine":
                                        Databases = helper.GetRelatedObjects(item.Id, "Microsoft.SQLServer.Library", "Microsoft.SQLServer.Database");
                                        break;

                                    case "Microsoft.SQLServer.2014.DBEngine":
                                        Databases = helper.GetRelatedObjects(item.Id, "Microsoft.SQLServer.2014.Discovery", "Microsoft.SQLServer.2014.Database");
                                        break;

                                    case "Microsoft.SQLServer.2016.DBEngine":
                                        Databases = helper.GetRelatedObjects(item.Id, "Microsoft.SQLServer.2016.Discovery", "Microsoft.SQLServer.2016.Database");
                                        break;

                                    default:
                                        break;
                                }

                                cmbDatabase.DataSource = new BindingSource(Databases, null);
                                cmbDatabase.DisplayMember = "DisplayName";
                                cmbDatabase.ValueMember = "Id";

                                ManagementPackClass winClass = helper.GetManagementPackClass("Microsoft.Windows.Library", "Microsoft.Windows.Computer");

                                principalName = helper.GetObjectsByName(item.Path, winClass).FirstOrDefault().DisplayName;
                            }
                        }
                    }
                }
            }
        }

        private string convertToUNSecureString(SecureString secstrPassword)
        {
            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secstrPassword);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            ValidatePageConfiguration();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(conStr);
            if (rbSQLAuth.Checked)
            {
                SecureData sd = ManagementGroup.Security.GetSecureData(runAsAccountList[cmbRunAsAccount.SelectedIndex].AccountStorageIdByteArray);
                sb.Append(string.Format("User Id={0};Password={1};", sd.Name, convertToUNSecureString(sd.Data)));
            }
            else
            {
                sb.Append("Integrated Security=SSPI;");
            }

            using (OleDbConnection con = new OleDbConnection(sb.ToString()))
            {
                using (OleDbCommand cmd = new OleDbCommand(txtQuery.Text, con))
                {
                    try
                    {
                        DataTable dt = new DataTable();
                        OleDbDataAdapter da = new OleDbDataAdapter(cmd);
                        da.Fill(dt);

                        if (dt.Rows.Count == 1 && dt.Columns.Count == 1)
                        {
                            double value;
                            if (double.TryParse(dt.Rows[0][0].ToString(), out value))
                            {
                                MessageBox.Show("return numeric value successfully test, result value " + value);
                            }
                            else
                            {
                                MessageBox.Show("return not numeric value rewrite query, result value " + dt.Rows[0][0].ToString());
                                IsConfigValid = false;
                                return;
                            }
                        }
                        else
                        {
                            MessageBox.Show("return more then one row or column");
                            IsConfigValid = false;
                            return;
                        }
                    }
                    catch (Exception EX)
                    {
                        MessageBox.Show("Error: " + EX.Message);
                        IsConfigValid = false;
                        return;
                    }
                }
            }
            IsConfigValid = ValidatePageConfiguration();
        }

        private void cmbDatabase_SelectedValueChanged(object sender, EventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            Database = (comboBox.SelectedItem as EnterpriseManagementObject).DisplayName;

            txtQuery.Enabled = true;
            btnTest.Enabled = true;
            cmbProvider.Enabled = true;
        }

        private void txtInstanceName_TextChanged(object sender, EventArgs e)
        {
            cmbDatabase.Enabled = true;
        }

        private void txtMetricType_TextChanged(object sender, EventArgs e)
        {
            IsConfigValid = ValidatePageConfiguration();
        }

        private void cmbProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.Append(string.Format("Provider={0};Server={1};Initial Catalog={2};", ProviderName, txtInstanceName.Text, (cmbDatabase.SelectedItem as EnterpriseManagementObject).DisplayName));

            rbWinAuth.Enabled = true;
            rbSQLAuth.Enabled = true;
            conStr = sb.ToString();
        }

        private void rbSQLAuth_CheckedChanged(object sender, EventArgs e)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(conStr);
            if (rbSQLAuth.Checked)
            {
                btnTest.Enabled = false;
                cmbRunAsAccount.Enabled = true;
                if (cmbRunAsAccount.SelectedIndex != -1)
                {
              
                    sb.Append(string.Format("User Id=$RunAs[Name=\"OleDb.{0}.SimpleAuthenticationAccount\"]/UserName$;Password=$RunAs[Name=\"OleDb.{0}.SimpleAuthenticationAccount\"]/Password$", templateIdString));
                    txtConnectionString.Text = sb.ToString();
                }
            }
            else
            {
                btnTest.Enabled = true;
                cmbRunAsAccount.Enabled = false;
            }
        }

        private void rbWinAuth_CheckedChanged(object sender, EventArgs e)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(conStr);
            if (rbWinAuth.Checked)
            {
                sb.Append("Integrated Security=SSPI;");
                txtConnectionString.Text = sb.ToString();
                cmbRunAsAccount.Enabled = false;
                btnTest.Enabled = true;
            }
            else
            {
                cmbRunAsAccount.Enabled = true;
                btnTest.Enabled = false;
            }
        }

        #endregion Events

        #region Private Methods

        private void SetSharedUserData()
        {
            SharedUserData["ConnectionAndQuery.TemplateIdString"] = templateIdString;
            SharedUserData["ConnectionAndQuery.UniqueID"] = UniqueID;
            SharedUserData["ConnectionAndQuery.Instance"] = txtInstanceName.Text;
            SharedUserData["ConnectionAndQuery.Database"] = Database;
            SharedUserData["ConnectionAndQuery.Query"] = txtQuery.Text;
            SharedUserData["ConnectionAndQuery.MetricType"] = txtMetricType.Text;
            SharedUserData["ConnectionAndQuery.PrincipalName"] = principalName;
            if (cmbRunAsAccount.SelectedIndex >= 0)
            {
                SharedUserData["ConnectionAndQuery.RunAsId"] = runAsAccountList[cmbRunAsAccount.SelectedIndex].AccountStorageIdString;
                SharedUserData["ConnectionAndQuery.RunAsName"] = runAsAccountList[cmbRunAsAccount.SelectedIndex].AccountName;
                SharedUserData["ConnectionAndQuery.RunAsSsid"] = runAsAccountList[cmbRunAsAccount.SelectedIndex].AccountStorageIdByteArray;
                SharedUserData["ConnectionAndQuery.RunAsAccount"] = runAsAccountList[cmbRunAsAccount.SelectedIndex].AccountStorageIdString;
            }
            SharedUserData["ConnectionAndQuery.ConnectionString"] = txtConnectionString.Text;
        }

        private bool ValidatePageConfiguration()
        {
            errorProvider.Clear();
            if (string.IsNullOrEmpty(txtInstanceName.Text))
            {
                errorProvider.SetError(txtInstanceName, string.Format(CultureInfo.CurrentUICulture, "BD Engine must be selected", new object[0]));

                return false;
            }
            else
            {
                if (string.IsNullOrEmpty(txtQuery.Text))
                {
                    errorProvider.SetError(txtQuery, string.Format(CultureInfo.CurrentUICulture, "Query invalid", new object[0]));
                    return false;
                }
            }
            if (string.IsNullOrEmpty(txtMetricType.Text))
            {
                errorProvider.SetError(txtMetricType, string.Format(CultureInfo.CurrentUICulture, "Give a name to query result", new object[0]));

                return false;
            }

            SetSharedUserData();
            return true;
        }

        #endregion Private Methods

        #region Constructor

        public ConnectionAndQuery()
        {
            InitializeComponent();
        }

        private void PopulateProvidersComboBox()
        {
            providers = new Dictionary<string, string>();
            OleDbDataReader rootEnumerator = OleDbEnumerator.GetRootEnumerator();
            if (rootEnumerator != null)
            {
                foreach (DbDataRecord dbDataRecord in rootEnumerator)
                {
                    string text = dbDataRecord[2] as string;
                    providers[text] = (dbDataRecord[0] as string);
                    int num = (int)dbDataRecord[3];
                    if ((!cmbProvider.Items.Contains(text) && 1 == num) || 3 == num)
                    {
                        cmbProvider.Items.Add(text);
                    }
                }
            }
        }

        private void PopulateRunAsComboBox()
        {
            if ((runAsAccountList != null) && (runAsAccountList.Count > 0))
            {
            }
            else
            {
                var monitoringSecureData = base.ManagementGroup.Security.GetSecureData();
                runAsAccountList = new List<RunAsAccount>();

                foreach (SecureData data in monitoringSecureData)
                {
                    if (data.SecureDataType.Equals(SecureDataType.Simple) || data.SecureDataType.Equals(SecureDataType.ActionAccount))
                    {
                        cmbRunAsAccount.Items.Add(data.Name);
                        runAsAccountList.Add(new RunAsAccount(data));
                    }
                }
            }
        }

        #endregion Constructor

        #region Public Methods

        public override bool OnSetActive()
        {
            PopulateProvidersComboBox();
            PopulateRunAsComboBox();
            return base.OnSetActive();
        }

        public override void LoadPageConfig()
        {
            if (IsCreationMode)
            {
                // in create mode init new id
                templateIdString = Guid.NewGuid().ToString("N");
                UniqueID = templateIdString;

                cmbDatabase.Enabled = false;
                txtQuery.Enabled = false;
                btnTest.Enabled = false;
                rbSQLAuth.Enabled = false;
                rbWinAuth.Enabled = false;
            }
            else
            {
                if (string.IsNullOrEmpty(InputConfigurationXml))
                {
                    return;
                }
                try
                {
                    ConnectionAndQueryConfig config = XmlHelper.Deserialize(InputConfigurationXml, typeof(ConnectionAndQueryConfig), true) as ConnectionAndQueryConfig;
                    Predicate<RunAsAccount> match = null;
                    Predicate<string> providerMatch = null;

                    PopulateProvidersComboBox();
                    PopulateRunAsComboBox();

                    templateIdString = config.TemplateIdString;
                    UniqueID = config.UniqueID;
                    txtInstanceName.Text = config.Instance;
                    txtQuery.Text = config.Query;
                    Database = config.Database;
                    cmbDatabase.Text = Database;
                    txtMetricType.Text = config.MetricType;
                    principalName = config.PrincipalName;

                    cmbDatabase.Enabled = false;
                    txtQuery.Enabled = false;
                    btnTest.Enabled = false;
                    rbSQLAuth.Enabled = false;
                    rbWinAuth.Enabled = false;
                    if (string.IsNullOrEmpty(config.RunAsAccount) || config.RunAsAccount == "01020202020202020202020202020202020202020200000000000000000000000000000000000000")
                    {
                        cmbRunAsAccount.SelectedIndex = -1;
                    }
                    else
                    {
                        if (match == null)
                        {
                            match = delegate (RunAsAccount simpleAccount)
                            {
                                return simpleAccount.AccountStorageIdString.Equals(config.RunAsAccount);
                            };
                        }
                        cmbRunAsAccount.SelectedIndex = runAsAccountList.FindIndex(match);
                    }

                    if (string.IsNullOrEmpty(config.ConnectionString))
                    {
                        cmbProvider.SelectedIndex = -1;
                    }
                    else
                    {
                        if (providerMatch == null)
                        {
                            providerMatch = delegate (string provider)
                              {
                                  return provider.Equals(config.ConnectionString.Split('=')[1]);
                              };
                        }
                        cmbProvider.SelectedIndex = providers.Keys.ToList<string>().FindIndex(providerMatch);
                        txtConnectionString.Text = config.ConnectionString;
                    }

                    SetSharedUserData();
                }
                catch (ArgumentNullException exception)
                {
                    return;
                }
                catch (InvalidOperationException exception2)
                {
                    return;
                }
            }
            base.IsConfigValid = ValidatePageConfiguration();
            base.LoadPageConfig();
        }

        public override bool SavePageConfig()
        {
            IsConfigValid = ValidatePageConfiguration();
            if (!IsConfigValid)
            {
                return false;
            }
            ConnectionAndQueryConfig config = new ConnectionAndQueryConfig();
            config.TemplateIdString = templateIdString;
            config.Instance = txtInstanceName.Text;
            config.Database = Database;
            config.Query = txtQuery.Text;
            config.UniqueID = UniqueID;
            config.MetricType = txtMetricType.Text;
            config.PrincipalName = principalName;
            if (cmbRunAsAccount.SelectedIndex >= 0)
            {
                config.RunAsAccount = runAsAccountList[cmbRunAsAccount.SelectedIndex].AccountStorageIdString;
            }
            else
            {
                config.RunAsAccount = "01020202020202020202020202020202020202020200000000000000000000000000000000000000";
            }
            config.ConnectionString = txtConnectionString.Text; //providers[cmbProvider.SelectedItem as string];

            OutputConfigurationXml = XmlHelper.Serialize(config, true);
            SetSharedUserData();
            return true;
        }

        #endregion Public Methods

        private void cmbRunAsAccount_SelectedIndexChanged(object sender, EventArgs e)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(conStr);
            if (rbSQLAuth.Checked)
            {
                btnTest.Enabled = false;
                cmbRunAsAccount.Enabled = true;
                if (cmbRunAsAccount.SelectedIndex != -1)
                {
                    sb.Append(string.Format("User Id=$RunAs[Name=\"OleDb.{0}.SimpleAuthenticationAccount\"]/UserName$;Password=$RunAs[Name=\"OleDb.{0}.SimpleAuthenticationAccount\"]/Password$", templateIdString));
                    txtConnectionString.Text = sb.ToString();
                }
            }
            else
            {
                btnTest.Enabled = true;
                cmbRunAsAccount.Enabled = false;
            }
        }
    }
}