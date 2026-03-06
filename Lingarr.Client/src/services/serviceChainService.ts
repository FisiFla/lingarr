import axios from 'axios'

export interface ServiceChainItem {
    serviceType: string
    monthlyLimitChars: number | null
    charsUsed: number
}

export interface ServiceChainEntry {
    serviceType: string
    monthlyLimitChars: number | null
}

export const serviceChainService = {
    async getChain(): Promise<ServiceChainItem[]> {
        const response = await axios.get<ServiceChainItem[]>('/api/service-chain')
        return response.data
    },

    async updateChain(services: ServiceChainEntry[]): Promise<void> {
        await axios.put('/api/service-chain', { services })
    },

    async getUsage(): Promise<Record<string, { used: number; limit: number | null }>> {
        const response = await axios.get('/api/service-chain/usage')
        return response.data
    }
}
