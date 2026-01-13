import React from 'react';
import { ApolloProvider, ApolloClient, InMemoryCache, createHttpLink } from '@apollo/client';
import { setContext } from '@apollo/client/link/context';
import { useAuth0 } from '@auth0/auth0-react';
import { Layout, Spin, Typography } from 'antd';
import Header from './components/Header';
import InvestigationPanel from './components/InvestigationPanel';
import './styles/auth.css';

const { Content } = Layout;
const { Title, Paragraph } = Typography;

const graphqlUri =
  import.meta.env.VITE_REACT_APP_API_URL ?? '/graphql';

const httpLink = createHttpLink({
  uri: graphqlUri,
  credentials: 'include'
});

const App: React.FC = () => {
  const { getAccessTokenSilently, isAuthenticated, isLoading, loginWithRedirect } = useAuth0();

  const authLink = setContext(async (_, { headers }) => {
    if (!isAuthenticated) {
      return { headers };
    }
    try {
      const token = await getAccessTokenSilently();
      return {
        headers: {
          ...headers,
          Authorization: `Bearer ${token}`
        }
      };
    } catch {
      return { headers };
    }
  });

  const client = new ApolloClient({
    link: authLink.concat(httpLink),
    cache: new InMemoryCache()
  });

  if (isLoading) {
    return (
      <div className="auth-gate">
        <Spin size="large" tip="Checking authentication..." />
      </div>
    );
  }

  return (
    <ApolloProvider client={client}>
      <Layout style={{ minHeight: '100vh' }}>
        <Header />
        <Content style={{ padding: '24px' }}>
          <InvestigationPanel />
        </Content>
      </Layout>
    </ApolloProvider>
  );
};

export default App;
