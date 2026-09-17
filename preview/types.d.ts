export interface Action {
  id: string;
  title: string;
  description: string;
  prompt: string;
  steps: string[];
  summary: string;
}

export interface Context {
  id: string;
  title: string;
  description: string;
  action: string;
}

export interface Catalog {
  locale: string;
  labels: Record<string, string>;
  actions: Action[];
  contexts: Context[];
}

export interface Command {
  id: string;
  title: string;
  description: string;
  kind: "action" | "context";
  route: string;
}

export interface InferenceConfig {
  model: string;
  system: string;
  languages: Record<string, string>;
  actions: Record<string, string>;
  formats: Record<string, string>;
  documents: string;
  documentPrompts: Record<string, string>;
}

export interface InferenceState {
  draft: string;
  output: string;
  model: string;
  status: string;
  rules: string[];
  documents: AttachedDocument[];
  documentStatus: string;
  documentBusy: boolean;
}

export interface AttachedDocument {
  name: string;
  text: string;
  truncated: boolean;
}

export interface ResponsePolicyConfig {
  version: string;
  maxCharacters: number;
  regenerationInstruction: string;
  forbiddenCharacters: string;
  numbersPattern: string;
  resultConsistency: {
    headingPrefix: string;
    conclusionPrefix: string;
    quantities: string[];
  };
  normalizations: { id: string; pattern: string; replacement: string }[];
  blockedPatterns: { id: string; pattern: string }[];
}

export interface PolicyInfo {
  version: string;
  changes: string[];
}

export interface PolicyResult extends PolicyInfo {
  text: string;
  reasons: string[];
  accepted: boolean;
}
