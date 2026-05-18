using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using SwiftPay.Constants.Enums;
using SwiftPay.DTOs.KycAuditRecordDTO;
using SwiftPay.DTOs.UserCustomerDTO;
using SwiftPay.Models;
using SwiftPay.Repositories.Interfaces;
using SwiftPay.Services.Interfaces;

namespace SwiftPay.Services
{
    public class KYCDocumentService : IKYCDocumentService
    {
        private readonly IKYCDocumentRepository _repo;
        private readonly IKYCRecordRepository _kycRepo;
        private readonly INotificationAlertService _notificationService;
        private readonly IMapper _mapper;

        public KYCDocumentService(IKYCDocumentRepository repo, IKYCRecordRepository kycRepo, INotificationAlertService notificationService, IMapper mapper)
        {
            _repo = repo;
            _kycRepo = kycRepo;
            _notificationService = notificationService;
            _mapper = mapper;
        }

        private async Task TryNotifyByKycIdAsync(int kycId, NotificationCategory category, string message)
        {
            try
            {
                var kycRecord = await _kycRepo.GetByIdAsync(kycId);
                if (kycRecord == null || kycRecord.UserID <= 0) return;
                await _notificationService.CreateAsync(new CreateNotificationDto
                {
                    UserID = kycRecord.UserID,
                    Message = message,
                    Category = category,
                });
            }
            catch { }
        }

        public async Task<KYCDocumentResponseDto> CreateAsync(CreateKYCDocumentDto dto)
        {
            var entity = _mapper.Map<KYCDocument>(dto);
            entity.UploadedDate = DateTime.UtcNow;
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.IsDeleted = false;
            // VerificationStatus uses DB default (Pending) — leave it unset
            var created = await _repo.CreateAsync(entity);
            await TryNotifyByKycIdAsync(created.KYCID, NotificationCategory.KYC,
                $"Your KYC document ({created.DocType}) has been uploaded successfully and is pending review.");
            return _mapper.Map<KYCDocumentResponseDto>(created);
        }

        public async Task<KYCDocumentResponseDto?> GetByIdAsync(int kycDocumentId)
        {
            var entity = await _repo.GetByIdAsync(kycDocumentId);
            return entity == null ? null : _mapper.Map<KYCDocumentResponseDto>(entity);
        }

        public async Task<IEnumerable<KYCDocumentResponseDto>> GetByKycIdAsync(int kycId)
        {
            var list = await _repo.GetByKycIdAsync(kycId);
            return list.Select(d => _mapper.Map<KYCDocumentResponseDto>(d));
        }

        public async Task<IEnumerable<KYCDocumentResponseDto>> GetAllAsync()
        {
            var list = await _repo.GetAllAsync();
            return list.Select(d => _mapper.Map<KYCDocumentResponseDto>(d));
        }

        public async Task<KYCDocumentResponseDto?> UpdateStatusAsync(int kycDocumentId, UpdateKYCDocumentStatusDto dto)
        {
            var entity = await _repo.GetByIdAsync(kycDocumentId);
            if (entity == null) return null;

            entity.VerificationStatus = dto.VerificationStatus;
            if (!string.IsNullOrWhiteSpace(dto.Notes))
                entity.Notes = dto.Notes;
            entity.UpdatedAt = DateTime.UtcNow;

            var updated = await _repo.UpdateAsync(entity);
            var docStatusMessage = updated.VerificationStatus switch
            {
                KycVerificationStatus.Verified => $"Your KYC document ({updated.DocType}) has been verified and approved.",
                KycVerificationStatus.Rejected => $"Your KYC document ({updated.DocType}) was rejected. Reason: {updated.Notes ?? "Please re-upload a valid document."}",
                _ => $"Your KYC document ({updated.DocType}) status has been updated to {updated.VerificationStatus}."
            };
            await TryNotifyByKycIdAsync(updated.KYCID, NotificationCategory.KYC, docStatusMessage);
            return _mapper.Map<KYCDocumentResponseDto>(updated);
        }

        public async Task<bool> DeleteAsync(int kycDocumentId) =>
            await _repo.DeleteAsync(kycDocumentId);
    }
}
