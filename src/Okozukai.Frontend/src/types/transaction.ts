export enum TransactionType {
    In = 0,
    Out = 1,
}

export interface TransactionResponse {
    id: string;
    journalId: string;
    journalName: string;
    currency: string;
    type: TransactionType;
    amount: number;
    occurredAt: string;
    note: string | null;
    tags: TagResponse[];
}

export interface CreateTransactionRequest {
    journalId: string;
    type: TransactionType;
    amount: number;
    occurredAt: string;
    note: string | null;
    tagIds: string[];
}

export interface UpdateTransactionRequest {
    type: TransactionType;
    amount: number;
    occurredAt: string;
    note: string | null;
    tagIds: string[];
}

export interface TransactionSummaryResponse {
    currency: string;
    totalIn: number;
    totalOut: number;
    net: number;
}

export interface TransactionPeriodRollupResponse {
    currency: string;
    opening: number;
    totalIn: number;
    totalOut: number;
    netChange: number;
    closing: number;
}

export interface TransactionMonthGroupResponse {
    year: number;
    month: number;
    transactions: TransactionResponse[];
    rollups: TransactionPeriodRollupResponse[];
}

export interface TransactionYearGroupResponse {
    year: number;
    months: TransactionMonthGroupResponse[];
    rollups: TransactionPeriodRollupResponse[];
}

export interface TransactionSpendingByTagItemResponse {
    tagId: string | null;
    tagName: string;
    totalOut: number;
}

export interface TransactionSpendingByTagResponse {
    currency: string;
    items: TransactionSpendingByTagItemResponse[];
}

export interface SpendingByTagMonthResponse {
    year: number;
    month: number;
    items: TransactionSpendingByTagItemResponse[];
}

export interface SpendingByTagMonthlyResponse {
    currency: string;
    months: SpendingByTagMonthResponse[];
}

export interface TagResponse {
    id: string;
    name: string;
    color: string;
}

export interface CreateTagRequest {
    name: string;
}

export interface UpdateTagRequest {
    name: string;
}

export interface JournalResponse {
    id: string;
    name: string;
    primaryCurrency: string;
    isClosed: boolean;
    createdAt: string;
}

export interface CreateJournalRequest {
    name: string;
    primaryCurrency: string;
}

export interface UpdateJournalRequest {
    name: string;
}
