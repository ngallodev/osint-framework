import { makeAutoObservable } from 'mobx';
import type { Investigation, InvestigationResult } from '../hooks/useInvestigations';

export class InvestigationStore {
  activeTab = '1';
  currentInvestigation: Investigation | null = null;
  errorMessage: string | null = null;
  investigationResults: InvestigationResult[] = [];

  constructor() {
    makeAutoObservable(this);
  }

  get hasInvestigation() {
    return Boolean(this.currentInvestigation);
  }

  setActiveTab = (tabKey: string) => {
    this.activeTab = tabKey;
  };

  setCurrentInvestigation = (investigation: Investigation | null) => {
    this.currentInvestigation = investigation;
    if (!investigation) {
      this.investigationResults = [];
    }
  };

  setErrorMessage = (message: string | null) => {
    this.errorMessage = message;
  };

  setInvestigationResults = (results: InvestigationResult[]) => {
    this.investigationResults = results;
  };

  clearInvestigation = () => {
    this.currentInvestigation = null;
    this.investigationResults = [];
    this.activeTab = '1';
  };
}

export class RootStore {
  readonly investigationStore: InvestigationStore;

  constructor() {
    this.investigationStore = new InvestigationStore();
  }
}
