using System;
using System.Windows.Forms;



namespace CloudRoboticsDefTool
{
    public partial class EditAppRoutingForm : Form
    {
        private string sqlConnectionString;
        private string encPassPhrase;
        private AppRoutingEntity appRoutingEntity;
        private bool createStatus;

        public EditAppRoutingForm(string sqlConnectionString, string encPassPhrase)
        {
            InitializeComponent();

            this.sqlConnectionString = sqlConnectionString;
            this.encPassPhrase = encPassPhrase;
            appRoutingEntity = new AppRoutingEntity();

            this.createStatus = true;
        }
        public EditAppRoutingForm(string sqlConnectionString, string encPassPhrase, AppRoutingEntity appRoutingEntity)
        {
            InitializeComponent();

            this.sqlConnectionString = sqlConnectionString;
            this.encPassPhrase = encPassPhrase;
            this.appRoutingEntity = appRoutingEntity;
            if (appRoutingEntity.AppId == string.Empty)
            {
                throw (new ApplicationException("App ID is nothing !!"));
            }
            if (appRoutingEntity.AppProcessingId == string.Empty)
            {
                throw (new ApplicationException("App Processing ID is nothing !!"));
            }

            this.createStatus = false;
        }

        private void EditAppRoutingForm_Load(object sender, EventArgs e)
        {
            if (createStatus)
            {
                createButton.Enabled = true;
                updateButton.Enabled = false;

                comboBoxStatus.DataSource = CRoboticsConst.StatusList;
            }
            else
            {
                createButton.Enabled = false;
                updateButton.Enabled = true;

                textBoxAppId.Text = appRoutingEntity.AppId;
                textBoxAppProcessingId.Text = appRoutingEntity.AppProcessingId;
                textBoxHttpUri.Text = appRoutingEntity.HttpUri;
                comboBoxStatus.DataSource = CRoboticsConst.StatusList;
                comboBoxStatus.Text = appRoutingEntity.Status;
                textBoxDesc.Text = appRoutingEntity.Description;
            }

        }

        private void createButton_Click(object sender, EventArgs e)
        {
            if (!checkInputData())
                return;

            try
            {
                AppRoutingEntity appRoutingEntity = getAppRoutingEntity();
                AppRoutingProcessor appRoutingProcessor = new AppRoutingProcessor(sqlConnectionString);
                appRoutingProcessor.insertAppRouting(appRoutingEntity);

                MessageBox.Show("App Routing created successfully!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            this.Close();
        }

        private void updateButton_Click(object sender, EventArgs e)
        {
            if (!checkInputData())
                return;

            try
            {
                AppRoutingEntity appRoutingEntity = getAppRoutingEntity();
                AppRoutingProcessor appRoutingProcessor = new AppRoutingProcessor(sqlConnectionString);
                appRoutingProcessor.updateAppRouting(appRoutingEntity);

                MessageBox.Show("App Routing updated successfully!", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            this.Close();

        }

        private AppRoutingEntity getAppRoutingEntity()
        {
            AppRoutingEntity appRoutingEntity = new AppRoutingEntity();
            appRoutingEntity.AppId = textBoxAppId.Text;
            appRoutingEntity.AppProcessingId = textBoxAppProcessingId.Text;
            appRoutingEntity.HttpUri = textBoxHttpUri.Text;
            appRoutingEntity.Status = comboBoxStatus.Text;
            appRoutingEntity.Description = textBoxDesc.Text;
            appRoutingEntity.Registered_DateTime = DateTime.Now;

            return appRoutingEntity;
        }
        private bool checkInputData()
        {
            if (textBoxAppId.Text == string.Empty)
            {
                MessageBox.Show("App ID is nothing !!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (textBoxAppProcessingId.Text == string.Empty)
            {
                MessageBox.Show("App Processing ID is nothing !!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (comboBoxStatus.Text == string.Empty)
            {
                MessageBox.Show("Status is nothing !!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void EditAppRoutingForm_Activated(object sender, EventArgs e)
        {
            textBoxAppId.Focus();
        }
    }
}
