import apiClient from './client';
import type {
    TransactionResponse,
    CreateTransactionRequest,
    UpdateTransactionRequest,
    TransactionSummaryResponse,
    TransactionYearGroupResponse,
    TransactionSpendingByTagResponse,
    SpendingByTagMonthlyResponse,
    TagResponse,
    CreateTagRequest,
    UpdateTagRequest
} from '../types/transaction';

type FilterParams = { journalId: string; from?: string; to?: string; tagIds?: string[]; noteSearch?: string };

export const transactionService = {
    async getAll(params: FilterParams & { page?: number; pageSize?: number }): Promise<TransactionResponse[]> {
        const response = await apiClient.get<TransactionResponse[]>('/api/transactions', { params });
        return response.data;
    },

    async getById(id: string): Promise<TransactionResponse> {
        const response = await apiClient.get<TransactionResponse>(`/api/transactions/${id}`);
        return response.data;
    },

    async create(request: CreateTransactionRequest): Promise<TransactionResponse> {
        const response = await apiClient.post<TransactionResponse>('/api/transactions', request);
        return response.data;
    },

    async update(id: string, request: UpdateTransactionRequest): Promise<TransactionResponse> {
        const response = await apiClient.put<TransactionResponse>(`/api/transactions/${id}`, request);
        return response.data;
    },

    async delete(id: string, journalId: string): Promise<void> {
        await apiClient.delete(`/api/transactions/${id}`, { params: { journalId } });
    },

    async getSummary(params: FilterParams): Promise<TransactionSummaryResponse> {
        const response = await apiClient.get<TransactionSummaryResponse>('/api/transactions/summary', { params });
        return response.data;
    },

    async getGrouped(params: FilterParams): Promise<TransactionYearGroupResponse[]> {
        const response = await apiClient.get<TransactionYearGroupResponse[]>('/api/transactions/grouped', { params });
        return response.data;
    },

    async getSpendingByTag(params: FilterParams): Promise<TransactionSpendingByTagResponse> {
        const response = await apiClient.get<TransactionSpendingByTagResponse>('/api/transactions/spending-by-tag', { params });
        return response.data;
    },

    async getSpendingByTagMonthly(params: FilterParams): Promise<SpendingByTagMonthlyResponse> {
        const response = await apiClient.get<SpendingByTagMonthlyResponse>('/api/transactions/spending-by-tag-monthly', { params });
        return response.data;
    },

    async getTags(): Promise<TagResponse[]> {
        const response = await apiClient.get<TagResponse[]>('/api/tags');
        return response.data;
    },

    async createTag(request: CreateTagRequest): Promise<TagResponse> {
        const response = await apiClient.post<TagResponse>('/api/tags', request);
        return response.data;
    },

    async updateTag(id: string, request: UpdateTagRequest): Promise<TagResponse> {
        const response = await apiClient.put<TagResponse>(`/api/tags/${id}`, request);
        return response.data;
    },

    async deleteTag(id: string): Promise<void> {
        await apiClient.delete(`/api/tags/${id}`);
    }
};
