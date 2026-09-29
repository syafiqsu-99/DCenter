import api from '@/utils/api'

export function useCrudApi(base) {
  return {
    list: (params) => api.get(base, params ? { params } : undefined).then(({ data }) => data),
    create: (body) => api.post(base, body).then(({ data }) => data),
    update: (id, body) => api.put(`${base}/${id}`, body),
    remove: (id) => api.delete(`${base}/${id}`),
    put: (path, body) => api.put(`${base}/${path}`, body),
    exportBlob: () => api.get(`${base}/export`, { responseType: 'blob' }).then(({ data }) => data),
    importCsv(file) {
      const form = new FormData()
      form.append('file', file)
      return api.post(`${base}/import`, form).then(({ data }) => data)
    },
  }
}
