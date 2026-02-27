import apiClient from './client';
import type { JournalResponse, CreateJournalRequest, UpdateJournalRequest } from '../types/transaction';

export const journalService = {
    async getAll(): Promise<JournalResponse[]> {
        const response = await apiClient.get<JournalResponse[]>('/api/journals');
        return response.data;
    },

    async getById(id: string): Promise<JournalResponse> {
        const response = await apiClient.get<JournalResponse>(`/api/journals/${id}`);
        return response.data;
    },

    async create(request: CreateJournalRequest): Promise<JournalResponse> {
        const response = await apiClient.post<JournalResponse>('/api/journals', request);
        return response.data;
    },

    async update(id: string, request: UpdateJournalRequest): Promise<JournalResponse> {
        const response = await apiClient.put<JournalResponse>(`/api/journals/${id}`, request);
        return response.data;
    },

    async delete(id: string): Promise<void> {
        await apiClient.delete(`/api/journals/${id}`);
    },

    async close(id: string): Promise<JournalResponse> {
        const response = await apiClient.post<JournalResponse>(`/api/journals/${id}/close`);
        return response.data;
    },

    async reopen(id: string): Promise<JournalResponse> {
        const response = await apiClient.post<JournalResponse>(`/api/journals/${id}/reopen`);
        return response.data;
    }
};
