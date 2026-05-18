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
    public class BeneficiaryService : IBeneficiaryService
    {
        private readonly IBeneficiaryRepository _repo;
        private readonly ICustomerRepository _customerRepo;
        private readonly IMapper _mapper;
        private readonly IAuditLogService _auditLogService;
        private readonly INotificationAlertService _notificationService;

        public BeneficiaryService(IBeneficiaryRepository repo, ICustomerRepository customerRepo, IMapper mapper, IAuditLogService auditLogService, INotificationAlertService notificationService)
        {
            _repo = repo;
            _customerRepo = customerRepo;
            _mapper = mapper;
            _auditLogService = auditLogService;
            _notificationService = notificationService;
        }

        private async Task TryNotifyByCustomerIdAsync(int customerId, string message)
        {
            try
            {
                var customer = await _customerRepo.GetByIdAsync(customerId);
                if (customer == null || customer.UserID <= 0) return;
                await _notificationService.CreateAsync(new CreateNotificationDto
                {
                    UserID = customer.UserID,
                    Message = message,
                    Category = NotificationCategory.Routing,
                });
            }
            catch { }
        }

        public async Task<BeneficiaryResponseDto> CreateAsync(CreateBeneficiaryDto dto)
        {
            // Validate that CustomerID is present
            var customerId = dto.CustomerID ?? throw new InvalidOperationException("CustomerID is required to create a beneficiary.");

            // Validate that Customer exists - BUSINESS LOGIC
            var customer = await _customerRepo.GetByIdAsync(customerId);
            if (customer == null)
                throw new KeyNotFoundException($"Customer with ID {customerId} does not exist.");

            // Use AutoMapper to map DTO to entity
            var entity = _mapper.Map<Beneficiary>(dto);

            var created = await _repo.CreateAsync(entity);
            try
            {
                await _auditLogService.CreateAsync(new DTOs.UserCustomerDTO.CreateAuditLogDto
                {
                    UserID = created.CustomerID,
                    Action = "Beneficiary.Create",
                    Resource = "Beneficiary",
                    Details = $"Beneficiary {created.BeneficiaryID} created for customer {created.CustomerID}."
                });
            }
            catch { }
            await TryNotifyByCustomerIdAsync(created.CustomerID,
                $"A new beneficiary '{created.Name}' has been added to your account successfully.");
            return _mapper.Map<BeneficiaryResponseDto>(created);
        }

        public async Task<BeneficiaryResponseDto> GetByIdAsync(int beneficiaryId)
        {
            var beneficiary = await _repo.GetByIdAsync(beneficiaryId);
            return _mapper.Map<BeneficiaryResponseDto>(beneficiary);
        }

        public async Task<IEnumerable<BeneficiaryResponseDto>> GetByCustomerIdAsync(int customerId)
        {
            var beneficiaries = await _repo.GetByCustomerIdAsync(customerId);
            return _mapper.Map<List<BeneficiaryResponseDto>>(beneficiaries);
        }

        public async Task<IEnumerable<BeneficiaryResponseDto>> GetAllAsync()
        {
            var beneficiaries = await _repo.GetAllAsync();
            return _mapper.Map<List<BeneficiaryResponseDto>>(beneficiaries);
        }

        public async Task<BeneficiaryResponseDto> UpdateAsync(int beneficiaryId, UpdateBeneficiaryDto dto)
        {
            var beneficiary = await _repo.GetByIdAsync(beneficiaryId);
            if (beneficiary == null)
                throw new KeyNotFoundException($"Beneficiary with ID {beneficiaryId} not found.");

            // Use AutoMapper to map only non-null fields
            _mapper.Map(dto, beneficiary);

            var updated = await _repo.UpdateAsync(beneficiary);
            try
            {
                await _auditLogService.CreateAsync(new DTOs.UserCustomerDTO.CreateAuditLogDto
                {
                    UserID = updated.CustomerID,
                    Action = "Beneficiary.Update",
                    Resource = "Beneficiary",
                    Details = $"Beneficiary {updated.BeneficiaryID} updated for customer {updated.CustomerID}."
                });
            }
            catch { }
            return _mapper.Map<BeneficiaryResponseDto>(updated);
        }

        public async Task<BeneficiaryResponseDto> UpdateVerificationStatusAsync(int beneficiaryId, UpdateBeneficiaryVerificationStatusDto dto)
        {
            var beneficiary = await _repo.GetByIdAsync(beneficiaryId);
            if (beneficiary == null)
                throw new KeyNotFoundException($"Beneficiary with ID {beneficiaryId} not found.");

            // Business logic: Update VerificationStatus
            beneficiary.VerificationStatus = dto.VerificationStatus;

            var updated = await _repo.UpdateAsync(beneficiary);
            try
            {
                await _auditLogService.CreateAsync(new DTOs.UserCustomerDTO.CreateAuditLogDto
                {
                    UserID = updated.CustomerID,
                    Action = "Beneficiary.UpdateVerification",
                    Resource = "Beneficiary",
                    Details = $"Beneficiary {updated.BeneficiaryID} verification changed to {updated.VerificationStatus}."
                });
            }
            catch { }
            var verificationMessage = updated.VerificationStatus switch
            {
                BeneficiaryVerificationStatus.Verified => $"Your beneficiary '{updated.Name}' has been verified and is ready for transfers.",
                BeneficiaryVerificationStatus.Rejected => $"Your beneficiary '{updated.Name}' could not be verified. Please review the details and try again.",
                _ => $"The verification status of beneficiary '{updated.Name}' has been updated to {updated.VerificationStatus}."
            };
            await TryNotifyByCustomerIdAsync(updated.CustomerID, verificationMessage);
            return _mapper.Map<BeneficiaryResponseDto>(updated);
        }

        public async Task<bool> DeleteAsync(int beneficiaryId)
        {
            var result = await _repo.DeleteAsync(beneficiaryId);
            if (result)
            {
                try
                {
                    await _auditLogService.CreateAsync(new DTOs.UserCustomerDTO.CreateAuditLogDto
                    {
                        UserID = beneficiaryId,
                        Action = "Beneficiary.Delete",
                        Resource = "Beneficiary",
                        Details = $"Beneficiary {beneficiaryId} deleted."
                    });
                }
                catch { }
            }
            return result;
        }
    }
}
