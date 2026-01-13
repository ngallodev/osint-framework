import React from 'react';
import { Layout, Typography } from 'antd';
import { useAuth0 } from '@auth0/auth0-react';
import ButtonGroup from 'antd/es/button/button-group';

const { Header: AntHeader } = Layout;
const { Title } = Typography;

const Header: React.FC = () => {
  const { loginWithRedirect, logout, isAuthenticated } = useAuth0();

  return (
    <AntHeader className="flex items-center bg-[#001529]">
      <Title level={3} className="!text-white !mb-0">
        OSINT Framework
      </Title>
      <ButtonGroup className="ml-auto flex gap-2">
        {!isAuthenticated && (
          <button
            onClick={() => loginWithRedirect()}
            className="bg-blue-500 text-white px-4 py-2 rounded"
          >
            Log In
          </button>
        )}
        {isAuthenticated && (
          <button
            onClick={() => logout({ logoutParams: { returnTo: window.location.origin } })}
            className="bg-red-500 text-white px-4 py-2 rounded"
          >
            Log Out
          </button>
        )}
      </ButtonGroup>
    </AntHeader>
  );
};

export default Header;
