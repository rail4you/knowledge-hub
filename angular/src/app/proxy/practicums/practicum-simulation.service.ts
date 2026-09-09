import type { CreatePracticumSimulationDto, PracticumSimulationDto, UpdatePracticumSimulationDto } from './simulations/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class PracticumSimulationService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreatePracticumSimulationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumSimulationDto>({
      method: 'POST',
      url: '/api/app/practicum-simulation',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/practicum-simulation/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAll = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumSimulationDto[]>({
      method: 'GET',
      url: '/api/app/practicum-simulation',
    },
    { apiName: this.apiName,...config });
  

  getListByProject = (projectId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumSimulationDto[]>({
      method: 'GET',
      url: `/api/app/practicum-simulation/by-project/${projectId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdatePracticumSimulationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PracticumSimulationDto>({
      method: 'PUT',
      url: `/api/app/practicum-simulation/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}