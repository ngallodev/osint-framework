import React, { useState, useEffect } from 'react';
import { observer } from 'mobx-react-lite';
import { useAuth0 } from '@auth0/auth0-react';
import {
  Card,
  Button,
  Alert,
  Spin,
  Select,
  Space,
  Typography,
  Table,
  Modal,
  Tag,
  Empty,
  Tooltip,
  Checkbox,
  Descriptions,
  Collapse
} from 'antd';
import { DeleteOutlined, ReloadOutlined, EyeOutlined, BugOutlined } from '@ant-design/icons';

const { Panel } = Collapse;
import {
  useQueueAiJob,
  useAiJobStatus,
  useAiJobHistory,
  useCancelAiJob,
  useRetryAiJob,
  type AiJob
} from '../hooks/useAiJobStatus';
import { useInvestigationStore } from '../stores';

const { Title, Paragraph, Text } = Typography;

const AiAnalysisPanelComponent: React.FC = () => {
  const { isAuthenticated } = useAuth0();
  const { currentInvestigation, investigationResults } = useInvestigationStore();
  const investigationId = currentInvestigation?.id;
  const results = investigationResults;
  const [activeJobId, setActiveJobId] = useState<number | null>(null);
  const [displayedResult, setDisplayedResult] = useState<AiJob | null>(null);
  const [selectedModel, setSelectedModel] = useState<string>('hf.co/TheBloke/Mistral-7B-Instruct-v0.2-GGUF:Q4_K_M');
  const [isResultModalVisible, setIsResultModalVisible] = useState(false);
  const [debugMode, setDebugMode] = useState<boolean>(false);

  const getResultSnippet = (job: AiJob): string | null => {
    if (job.result && job.result.trim().length > 0) {
      const trimmed = job.result.trim();
      return trimmed.length > 80 ? `${trimmed.slice(0, 80)}…` : trimmed;
    }

    const firstSection = job.structuredResult?.sections?.[0];
    if (firstSection && firstSection.content.trim().length > 0) {
      const trimmed = firstSection.content.trim();
      return trimmed.length > 80 ? `${trimmed.slice(0, 80)}…` : trimmed;
    }

    return null;
  };

  const hasResultData = (job: AiJob): boolean => {
    if (job.result && job.result.trim().length > 0) {
      return true;
    }

    return Boolean(job.structuredResult?.sections && job.structuredResult.sections.length > 0);
  };

  const renderResultContent = (job: AiJob) => {
    if (job.structuredResult?.sections && job.structuredResult.sections.length > 0) {
      return job.structuredResult.sections.map((section) => (
        <div key={section.key} style={{ marginBottom: 16 }}>
          <Title level={5}>{section.heading}</Title>
          <Paragraph style={{ whiteSpace: 'pre-wrap' }}>{section.content || '—'}</Paragraph>
        </div>
      ));
    }

    if (job.result) {
      return (
        <Paragraph style={{ whiteSpace: 'pre-wrap', maxHeight: 400, overflow: 'auto' }}>
          {job.result}
        </Paragraph>
      );
    }

    return <Text>No result available yet.</Text>;
  };

  // Available models (hardcoded for now, can be populated from backend)
  const availableModels = [
    { label: 'Mistral-7B', value: 'hf.co/TheBloke/Mistral-7B-Instruct-v0.2-GGUF:Q4_K_M' },
    { label: 'Llama2', value: 'llama2' },
    { label: 'Neural Chat', value: 'neural-chat' }
  ];

  // Hooks for job operations
  const { queueJob, loading: queueLoading } = useQueueAiJob();
  const { job: currentJob, isPolling, loading: pollingLoading, isTerminal } = useAiJobStatus(activeJobId || undefined);
  const { jobs: jobHistory, refetch: refetchHistory } = useAiJobHistory(investigationId, 20);
  const { cancelJob, loading: cancelLoading } = useCancelAiJob();
  const { retryJob, loading: retryLoading } = useRetryAiJob();

  // Update displayed result when current job completes
  useEffect(() => {
    if (currentJob && isTerminal) {
      setDisplayedResult(currentJob);
      refetchHistory();
    }
  }, [currentJob, isTerminal, refetchHistory]);

  // Handlers
  const handleQueueJob = async (jobType: 'analysis' | 'inference') => {
    if (!investigationId) {
      alert('Please start an investigation before running AI analysis jobs.');
      return;
    }
    if (!results || results.length === 0) {
      alert('Please add results to the investigation first');
      return;
    }

    const result = await queueJob(investigationId, jobType, selectedModel, null, debugMode);
    if (result.success) {
      setActiveJobId(result.jobId);
    } else {
      alert(`Failed to queue job: ${result.error}`);
    }
  };

  const handleCancelJob = async (jobId: number) => {
    const confirmed = window.confirm('Are you sure you want to cancel this job?');
    if (!confirmed) return;

    const result = await cancelJob(jobId);
    if (result.success) {
      if (activeJobId === jobId) {
        setActiveJobId(null);
      }
      refetchHistory();
    } else {
      alert(`Failed to cancel job: ${result.message}`);
    }
  };

  const handleRetryJob = async (jobId: number) => {
    const result = await retryJob(jobId);
    if (result.success) {
      setActiveJobId(result.jobId);
    } else {
      alert(`Failed to retry job: ${result.error}`);
    }
  };

  const handleViewResult = (job: AiJob) => {
    setDisplayedResult(job);
    setIsResultModalVisible(true);
  };

  // Status badge styling
  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Succeeded':
        return 'success';
      case 'Failed':
        return 'error';
      case 'Running':
        return 'processing';
      case 'Queued':
        return 'default';
      case 'Cancelled':
        return 'default';
      default:
        return 'default';
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'Succeeded':
        return '🟢';
      case 'Failed':
        return '🔴';
      case 'Running':
        return '🟠';
      case 'Queued':
        return '🔵';
      case 'Cancelled':
        return '⚫';
      default:
        return '⚪';
    }
  };

  // Job history table columns
  const columns = [
    {
      title: 'Type',
      dataIndex: 'jobType',
      key: 'jobType',
      render: (text: string) => (text === 'analysis' ? 'Analysis' : 'Inference')
    },
    {
      title: 'Status',
      dataIndex: 'status',
      key: 'status',
      render: (status: string) => (
        <Tag color={getStatusColor(status)}>
          {getStatusIcon(status)} {status}
        </Tag>
      )
    },
    {
      title: 'Model',
      dataIndex: 'model',
      key: 'model',
      render: (model: string | null | undefined) =>
        model ? <Tooltip title={model}>{model.substring(0, 30)}...</Tooltip> : '—',
      width: 200
    },
    {
      title: 'Created',
      dataIndex: 'createdAt',
      key: 'createdAt',
      render: (date: string) => new Date(date).toLocaleString(),
      width: 150
    },
    {
      title: 'Completed',
      dataIndex: 'completedAt',
      key: 'completedAt',
      render: (date: string | null) => (date ? new Date(date).toLocaleString() : '—'),
      width: 150
    },
    {
      title: 'Result',
      key: 'result',
      render: (_: unknown, record: AiJob) => {
        const snippet = getResultSnippet(record);
        if (!snippet) return '—';
        return (
          <Tooltip title="Click to view full result">
            {snippet}
          </Tooltip>
        );
      },
      width: 200
    },
    {
      title: 'Actions',
      key: 'actions',
      render: (_: any, record: AiJob) => (
        <Space size="small">
          {record.result && (
            <Tooltip title="View full result">
              <Button
                type="text"
                size="small"
                icon={<EyeOutlined />}
                onClick={() => handleViewResult(record)}
              />
            </Tooltip>
          )}
          {record.status === 'Queued' || record.status === 'Running' ? (
            <Tooltip title="Cancel job">
              <Button
                type="text"
                size="small"
                danger
                icon={<DeleteOutlined />}
                loading={cancelLoading}
                onClick={() => handleCancelJob(record.id)}
              />
            </Tooltip>
          ) : record.status === 'Failed' ? (
            <Tooltip title="Retry job">
              <Button
                type="text"
                size="small"
                icon={<ReloadOutlined />}
                loading={retryLoading}
                onClick={() => handleRetryJob(record.id)}
              />
            </Tooltip>
          ) : null}
        </Space>
      )
    }
  ];

  const canQueueJob = results.length > 0 && !activeJobId && isAuthenticated;

  return (
    <Card title="AI Analysis" style={{ marginTop: 16 }}>
      <Space direction="vertical" style={{ width: '100%' }} size="middle">
        {!isAuthenticated && (
          <Alert
            message="Login Required"
            description="Please log in to run AI analysis jobs."
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
          />
        )}
        {/* Model Selection */}
        <div>
          <Title level={5}>AI Model Selection</Title>
          <Select
            value={selectedModel}
            onChange={setSelectedModel}
            style={{ width: 300 }}
            options={availableModels}
            disabled={!canQueueJob}
          />
        </div>

        {/* Debug Mode Checkbox */}
        <div>
          <Checkbox
            checked={debugMode}
            onChange={(e) => setDebugMode(e.target.checked)}
            disabled={!canQueueJob}
          >
            <Space>
              <BugOutlined />
              <span>Debug Mode</span>
              <Tooltip title="Capture detailed timing metrics, token counts, and full prompt text">
                <Text type="secondary" style={{ fontSize: '12px' }}>(capture metrics)</Text>
              </Tooltip>
            </Space>
          </Checkbox>
        </div>

        {/* Action Buttons */}
        <Space>
          <Button
            type="primary"
            onClick={() => handleQueueJob('analysis')}
            loading={queueLoading}
            disabled={!canQueueJob}
          >
            Queue Analysis
          </Button>
          <Button
            onClick={() => handleQueueJob('inference')}
            loading={queueLoading}
            disabled={!canQueueJob}
          >
            Queue Inference
          </Button>
        </Space>

        {/* Current Job Status Alert */}
        {activeJobId && currentJob && (
          <Alert
            message={`Job Status: ${currentJob.status}`}
            description={
              <>
                <div>Job #: {currentJob.id}</div>
                <div>Model: {currentJob.model}</div>
                {(currentJob.errorInfo?.message ?? currentJob.error) && (
                  <div style={{ color: '#ff4d4f' }}>
                    Error: {currentJob.errorInfo?.message ?? currentJob.error}
                    {currentJob.errorInfo?.code && (
                      <span> ({currentJob.errorInfo.code})</span>
                    )}
                  </div>
                )}
              </>
            }
            type={
              currentJob.status === 'Succeeded'
                ? 'success'
                : currentJob.status === 'Failed'
                ? 'error'
                : 'info'
            }
            showIcon
            style={{ marginBottom: 16 }}
            action={
              isPolling && (
                <Space>
                  <Spin size="small" />
                  <Text>Polling...</Text>
                </Space>
              )
            }
          />
        )}

        {/* Displayed Result */}
        {displayedResult && displayedResult.status === 'Succeeded' && (
          <Card type="inner" title={`AI ${displayedResult.jobType === 'analysis' ? 'Analysis' : 'Inference'} Result`}>
            {renderResultContent(displayedResult)}
            <Text type="secondary">
              Generated at: {new Date(displayedResult.completedAt!).toLocaleString()} using {displayedResult.model}
            </Text>
          </Card>
        )}

        {/* Job History Table */}
        <div>
          <Space style={{ marginBottom: 16 }}>
            <Title level={5} style={{ margin: 0 }}>Job History</Title>
            <Button
              type="text"
              size="small"
              icon={<ReloadOutlined />}
              onClick={() => refetchHistory()}
            />
          </Space>
          {jobHistory && jobHistory.length > 0 ? (
            <Table
              dataSource={jobHistory}
              columns={columns}
              rowKey="id"
              size="small"
              pagination={{ pageSize: 10 }}
              onRow={(record) => ({
                style: { cursor: hasResultData(record) ? 'pointer' : 'default' },
                onClick: () => {
                  if (hasResultData(record)) {
                    handleViewResult(record);
                  }
                }
              })}
            />
          ) : (
            <Empty description="No jobs yet" />
          )}
        </div>
      </Space>

      {/* Result Modal */}
      <Modal
        title={`Full Result: ${displayedResult?.jobType === 'analysis' ? 'Analysis' : 'Inference'}`}
        open={isResultModalVisible}
        onCancel={() => setIsResultModalVisible(false)}
        footer={null}
        width={900}
      >
        {displayedResult && (
          <>
            <Paragraph>
              <Text strong>Job ID:</Text> {displayedResult.id}
            </Paragraph>
            <Paragraph>
              <Text strong>Status:</Text> <Tag color={getStatusColor(displayedResult.status)}>{displayedResult.status}</Tag>
            </Paragraph>
            <Paragraph>
              <Text strong>Model:</Text> {displayedResult.model}
            </Paragraph>
            <Paragraph>
              <Text strong>Created:</Text> {new Date(displayedResult.createdAt).toLocaleString()}
            </Paragraph>
            {displayedResult.completedAt && (
              <Paragraph>
                <Text strong>Completed:</Text> {new Date(displayedResult.completedAt).toLocaleString()}
              </Paragraph>
            )}
            {(displayedResult.errorInfo?.message || displayedResult.error) && (
              <Alert
                message="Error"
                description={displayedResult.errorInfo?.message ?? displayedResult.error}
                type="error"
                style={{ marginBottom: 16 }}
              />
            )}
            <Card type="inner" title="Result Content">
              {renderResultContent(displayedResult)}
              {displayedResult.structuredResult?.metadata && Object.keys(displayedResult.structuredResult.metadata).length > 0 && (
                <Paragraph style={{ marginTop: 16 }}>
                  <Text type="secondary">Metadata: </Text>
                  <code>{JSON.stringify(displayedResult.structuredResult.metadata)}</code>
                </Paragraph>
              )}
            </Card>

            {/* Debug Information Section */}
            {displayedResult.debug && displayedResult.debugInfo && (
              <Card type="inner" title={<><BugOutlined /> Debug Information</>} style={{ marginTop: 16 }}>
                <Descriptions bordered column={2} size="small">
                  <Descriptions.Item label="Prompt Length">
                    {displayedResult.debugInfo.promptLength} characters
                  </Descriptions.Item>
                  <Descriptions.Item label="Total Duration">
                    {displayedResult.debugInfo.ollamaMetrics?.totalDurationNs
                      ? (displayedResult.debugInfo.ollamaMetrics.totalDurationNs / 1_000_000_000).toFixed(2) + 's'
                      : '—'}
                  </Descriptions.Item>
                  <Descriptions.Item label="Prompt Tokens">
                    {displayedResult.debugInfo.ollamaMetrics?.promptEvalCount ?? '—'} tokens
                  </Descriptions.Item>
                  <Descriptions.Item label="Prompt Speed">
                    {displayedResult.debugInfo.ollamaMetrics?.promptTokensPerSecond?.toFixed(2) ?? '—'} tokens/sec
                  </Descriptions.Item>
                  <Descriptions.Item label="Response Tokens">
                    {displayedResult.debugInfo.ollamaMetrics?.evalCount ?? '—'} tokens
                  </Descriptions.Item>
                  <Descriptions.Item label="Response Speed">
                    {displayedResult.debugInfo.ollamaMetrics?.responseTokensPerSecond?.toFixed(2) ?? '—'} tokens/sec
                  </Descriptions.Item>
                  <Descriptions.Item label="HTTP Request Time">
                    {displayedResult.debugInfo.httpMetrics?.requestDurationMs?.toFixed(0) ?? '—'}ms
                  </Descriptions.Item>
                  <Descriptions.Item label="Done Reason">
                    {displayedResult.debugInfo.ollamaMetrics?.doneReason ?? '—'}
                  </Descriptions.Item>
                </Descriptions>

                <Collapse style={{ marginTop: 16 }}>
                  <Panel header="Full Prompt" key="1">
                    <pre style={{ whiteSpace: 'pre-wrap', maxHeight: 300, overflow: 'auto' }}>
                      {displayedResult.debugInfo.promptText}
                    </pre>
                  </Panel>
                </Collapse>
              </Card>
            )}
          </>
        )}
      </Modal>
    </Card>
  );
};

export default observer(AiAnalysisPanelComponent);
