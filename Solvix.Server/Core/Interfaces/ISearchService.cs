using Solvix.Server.Application.DTOs;

namespace Solvix.Server.Core.Interfaces
{
    public interface ISearchService
    {
        Task<List<SearchResultDto>> SearchAsync(string query, long currentUserId);
    }
}