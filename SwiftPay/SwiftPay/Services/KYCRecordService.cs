using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using SwiftPay.Services.Interfaces;
using SwiftPay.Repositories.Interfaces;
using SwiftPay.DTOs.UserCustomerDTO;
using SwiftPay.Constants.Enums;
using SwiftPay.Domain.Remittance.Entities;

namespace SwiftPay.Services
{
    public class KYCRecordService : IKYCRecordService
    {
        private readonly IKYCRecordRepository _repo;
        private readonly IUserRepository _userRepo;
        private readonly IAuditLogService _auditLogService;
        private readonly INotificationAlertService _notificationService;
        private readonly IMapper _mapper;

        public KYCRecordService(IKYCRecordRepository repo, IUserRepository userRepo, IAuditLogService auditLogService, INotificationAlertService notificationService, IMapper mapper)
        {
            _repo = repo;
            _userRepo = userRepo;
            _auditLogService = auditLogService;
            _notificationService = notificationService;
            _mapper = mapper;
        }

        private async Task TryNotifyAsync(int userId, NotificationCategory category, string message)
        {
            if (userId <= 0) return;
            try
            {
                await _notificationService.CreateAsync(new CreateNotificationDto
                {
                    UserID = userId,
                    Message = message,
                    Category = category,
                });
            }
            catch { }
        }

        public async Task<KYCRecordResponseDto> CreateAsync(CreateKYCRecordDto dto)
        {
            // Ensure UserID is provided (controller should set this from JWT for non-privileged callers)
            if (!dto.UserID.HasValue)
                throw new KeyNotFoundException("UserID not provided.");

            var userId = dto.UserID.Value;

            // Validate that User exists - BUSINESS LOGIC
            var user = await _userRepo.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} does not exist.");

            // Check if KYC record already exists for this user - BUSINESS LOGIC
            var existingKyc = await _repo.GetByUserIdAsync(userId);
            if (existingKyc != null)
                throw new InvalidOperationException($"A KYC record already exists for User ID {userId}.");

            // Use AutoMapper to map DTO to entity
            var entity = _mapper.Map<KYCRecord>(dto);

            var created = await _repo.CreateAsync(entity);
            try
            {
                await _auditLogService.CreateAsync(new DTOs.UserCustomerDTO.CreateAuditLogDto
                {
                    UserID = created.UserID,
                    Action = "KYC.Create",
                    Resource = "KYCRecord",
                    Details = $"KYC record {created.KYCID} created for user {created.UserID}."
                });
            }
            catch { }
            await TryNotifyAsync(created.UserID, NotificationCategory.KYC,
                "Your KYC record has been submitted successfully and is pending review.");
            return _mapper.Map<KYCRecordResponseDto>(created);
        }

        public async Task<KYCRecordResponseDto> GetByIdAsync(int kycId)
        {
            var kyc = await _repo.GetByIdAsync(kycId);
            return _mapper.Map<KYCRecordResponseDto>(kyc);
        }

        public async Task<KYCRecordResponseDto> GetByUserIdAsync(int userId)
        {
            var kyc = await _repo.GetByUserIdAsync(userId);
            return _mapper.Map<KYCRecordResponseDto>(kyc);
        }

        public async Task<IEnumerable<KYCRecordResponseDto>> GetAllAsync()
        {
            var kycRecords = await _repo.GetAllAsync();
            return _mapper.Map<List<KYCRecordResponseDto>>(kycRecords);
        }

        public async Task<KYCRecordResponseDto> UpdateAsync(int kycId, UpdateKYCRecordDto dto)
        {
            var kyc = await _repo.GetByIdAsync(kycId);
            if (kyc == null)
                throw new KeyNotFoundException($"KYC Record with ID {kycId} not found.");

            // Use AutoMapper to map only non-null fields
            _mapper.Map(dto, kyc);

            var updated = await _repo.UpdateAsync(kyc);
            try
            {
                await _auditLogService.CreateAsync(new DTOs.UserCustomerDTO.CreateAuditLogDto
                {
                    UserID = updated.UserID,
                    Action = "KYC.Update",
                    Resource = "KYCRecord",
                    Details = $"KYC record {updated.KYCID} updated for user {updated.UserID}."
                });
            }
            catch { }
            return _mapper.Map<KYCRecordResponseDto>(updated);
        }

        public async Task<KYCRecordResponseDto> UpdateStatusAsync(int kycId, UpdateKycStatusDto dto)
        {
            var kyc = await _repo.GetByIdAsync(kycId);
            if (kyc == null)
                throw new KeyNotFoundException($"KYC Record with ID {kycId} not found.");

            // Business logic: Update VerificationStatus and optional Notes
            kyc.VerificationStatus = dto.VerificationStatus;
            if (!string.IsNullOrEmpty(dto.Notes))
                kyc.Notes = dto.Notes;

            // VerifiedDate and UpdatedAt are handled by AuditLogInterceptor automatically
            var updated = await _repo.UpdateAsync(kyc);
            try
            {
                await _auditLogService.CreateAsync(new DTOs.UserCustomerDTO.CreateAuditLogDto
                {
                    UserID = updated.UserID,
                    Action = "KYC.UpdateStatus",
                    Resource = "KYCRecord",
                    Details = $"KYC record {updated.KYCID} status set to {updated.VerificationStatus}."
                });
            }
            catch { }
            var statusMessage = updated.VerificationStatus switch
            {
                KycVerificationStatus.Verified => "Your KYC verification has been approved. You now have full access to all features.",
                KycVerificationStatus.Rejected => $"Your KYC verification was rejected. Reason: {updated.Notes ?? "Please contact support for details."}",
                _ => $"Your KYC status has been updated to {updated.VerificationStatus}."
            };
            await TryNotifyAsync(updated.UserID, NotificationCategory.KYC, statusMessage);
            return _mapper.Map<KYCRecordResponseDto>(updated);
        }

        public async Task<KYCRecordResponseDto> MarkAsVerifiedAsync(int kycId)
        {
            var kyc = await _repo.GetByIdAsync(kycId);
            if (kyc == null)
                throw new KeyNotFoundException($"KYC Record with ID {kycId} not found.");

            kyc.VerificationStatus = KycVerificationStatus.Verified;
            // VerifiedDate and UpdatedAt are handled by AuditLogInterceptor automatically
            var updated = await _repo.UpdateAsync(kyc);
            try
            {
                await _auditLogService.CreateAsync(new DTOs.UserCustomerDTO.CreateAuditLogDto
                {
                    UserID = updated.UserID,
                    Action = "KYC.MarkVerified",
                    Resource = "KYCRecord",
                    Details = $"KYC record {updated.KYCID} marked verified for user {updated.UserID}."
                });
            }
            catch { }
            await TryNotifyAsync(updated.UserID, NotificationCategory.KYC,
                "Congratulations! Your KYC verification has been approved. You now have full access to all features.");

            return _mapper.Map<KYCRecordResponseDto>(updated);
        }

        public async Task<KYCRecordListDto> GetPendingAsync(int pageNumber = 1, int pageSize = 10)
        {
            // Business logic: Retrieve only pending KYC records
            var allRecords = await _repo.GetAllAsync();
            
            var pendingRecords = allRecords
                .Where(kyc => kyc.VerificationStatus == KycVerificationStatus.Pending)
                .OrderBy(kyc => kyc.CreatedAt)
                .ToList();

            var totalCount = pendingRecords.Count;
            var pagedRecords = pendingRecords
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new KYCRecordListDto
            {
                Records = _mapper.Map<List<KYCRecordResponseDto>>(pagedRecords),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<bool> DeleteAsync(int kycId)
        {
            var result = await _repo.DeleteAsync(kycId);
            if (result)
            {
                try
                {
                    await _auditLogService.CreateAsync(new DTOs.UserCustomerDTO.CreateAuditLogDto
                    {
                        UserID = kycId,
                        Action = "KYC.Delete",
                        Resource = "KYCRecord",
                        Details = $"KYC record {kycId} deleted."
                    });
                }
                catch { }
            }
            return result;
        }
    }
}
