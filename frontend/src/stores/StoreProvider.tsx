import React, { createContext, useContext, useRef, type ReactNode } from 'react';
import { RootStore } from './InvestigationStore';

const StoreContext = createContext<RootStore | null>(null);

export const StoreProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const storeRef = useRef<RootStore>();

  if (!storeRef.current) {
    storeRef.current = new RootStore();
  }

  return <StoreContext.Provider value={storeRef.current}>{children}</StoreContext.Provider>;
};

export const useRootStore = () => {
  const context = useContext(StoreContext);

  if (!context) {
    throw new Error('useRootStore must be used within a StoreProvider');
  }

  return context;
};

export const useInvestigationStore = () => useRootStore().investigationStore;
