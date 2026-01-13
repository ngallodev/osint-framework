import React, { useEffect } from 'react';
import { Card, Button, Form, Input, Select, Tabs, Alert, Spin } from 'antd';
import { observer } from 'mobx-react-lite';
import { useAuth0 } from '@auth0/auth0-react';
import AiAnalysisPanel from './AiAnalysisPanel';
import { useCreateInvestigation } from '../hooks/useInvestigations';
import { useResultsByInvestigation } from '../hooks/useResults';
import { useInvestigationStore } from '../stores';

const { Option } = Select;
const { TabPane } = Tabs;

interface InvestigationFormValues {
  target: string;
  investigationType: string;
  requestedBy?: string;
}

const InvestigationPanelComponent: React.FC = () => {
  const [form] = Form.useForm<InvestigationFormValues>();
  const { isAuthenticated } = useAuth0();
  const {
    activeTab,
    setActiveTab,
    currentInvestigation,
    setCurrentInvestigation,
    errorMessage,
    setErrorMessage,
    investigationResults,
    setInvestigationResults
  } = useInvestigationStore();

  const { createInvestigation, loading: creatingInvestigation } = useCreateInvestigation();
  const {
    results: fetchedResults,
    loading: loadingResults,
    refetch: refetchResults
  } = useResultsByInvestigation(currentInvestigation?.id);

  useEffect(() => {
    if (!loadingResults) {
      setInvestigationResults(fetchedResults ?? []);
    }
  }, [fetchedResults, loadingResults, setInvestigationResults]);

  const onFinish = async (values: InvestigationFormValues) => {
    setErrorMessage(null);

    const result = await createInvestigation(
      values.target,
      values.investigationType,
      values.requestedBy ?? null
    );

    if (!result.success) {
      const error = result.error ?? 'Failed to create investigation';
      setErrorMessage(error);
      console.error('Failed to create investigation:', error);
      return;
    }

    setCurrentInvestigation(result.data);
    setActiveTab('2');

    setTimeout(() => {
      refetchResults();
    }, 500);
  };

  const results = investigationResults ?? [];

  return (
    <Card title="OSINT Investigation Framework" style={{ maxWidth: 800, margin: '0 auto' }}>
      {errorMessage && (
        <Alert
          message="Error"
          description={errorMessage}
          type="error"
          closable
          style={{ marginBottom: 16 }}
          onClose={() => setErrorMessage(null)}
        />
      )}

      <Tabs activeKey={activeTab} onChange={setActiveTab}>
        <TabPane tab="New Investigation" key="1">
          {!isAuthenticated && (
            <Alert
              message="Login Required"
              description="Please log in to create new investigations."
              type="info"
              showIcon
              style={{ marginBottom: 16 }}
            />
          )}
          <Form form={form} layout="vertical" onFinish={onFinish}>
            <Form.Item
              name="target"
              label="Target"
              rules={[{ required: true, message: 'Please enter a target' }]}
            >
              <Input placeholder="Enter domain, username, email, or IP address" disabled={!isAuthenticated} />
            </Form.Item>

            <Form.Item
              name="investigationType"
              label="Investigation Type"
              rules={[{ required: true, message: 'Please select investigation type' }]}
            >
              <Select placeholder="Select investigation type" disabled={!isAuthenticated}>
                <Option value="domain">Domain Intelligence</Option>
                <Option value="username">Username Enumeration</Option>
                <Option value="email">Email Investigation</Option>
                <Option value="comprehensive">Comprehensive Analysis</Option>
              </Select>
            </Form.Item>

            <Form.Item name="requestedBy" label="Requested By (Optional)">
              <Input placeholder="Enter your name or identifier" disabled={!isAuthenticated} />
            </Form.Item>

            <Form.Item>
              <Button type="primary" htmlType="submit" loading={creatingInvestigation} disabled={!isAuthenticated}>
                {creatingInvestigation ? 'Creating Investigation...' : 'Start Investigation'}
              </Button>
            </Form.Item>
          </Form>
        </TabPane>

        <TabPane tab="AI Analysis" key="2" disabled={!currentInvestigation}>
          {currentInvestigation && <AiAnalysisPanel />}
        </TabPane>

        <TabPane tab="Results" key="3" disabled={!currentInvestigation}>
          {currentInvestigation && (
            <div>
              <h3>Investigation Results</h3>
              {loadingResults ? (
                <div className="text-center py-5">
                  <Spin size="large" />
                </div>
              ) : results.length > 0 ? (
                <div>
                  <p>Total results: {results.length}</p>
                  <pre style={{ maxHeight: 400, overflow: 'auto' }}>
                    {JSON.stringify(results, null, 2)}
                  </pre>
                </div>
              ) : (
                <p>
                  No results found for this investigation. Results will appear once tools are run and data is
                  ingested.
                </p>
              )}
            </div>
          )}
        </TabPane>
      </Tabs>
    </Card>
  );
};

export default observer(InvestigationPanelComponent);
