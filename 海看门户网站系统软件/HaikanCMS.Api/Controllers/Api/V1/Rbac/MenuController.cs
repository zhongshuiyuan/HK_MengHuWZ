using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using AutoMapper;
using HaikanCMS.Api.Entities;
using HaikanCMS.Api.Entities.Enums;
using HaikanCMS.Api.Extensions;
using HaikanCMS.Api.Extensions.AuthContext;
using HaikanCMS.Api.Extensions.CustomException;
using HaikanCMS.Api.Models.Response;
using HaikanCMS.Api.RequestPayload.Rbac.Menu;
using HaikanCMS.Api.ViewModels.Rbac.SystemMenu;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static HaikanCMS.Api.Entities.SystemUser;
using HaikanCMS.Api.logs.TolLog;

namespace HaikanEnrollmentRegistration.Api.Controllers.Api.V1.Rbac
{
    /// <summary>
    /// 
    /// </summary>
    //[CustomAuthorize]
    [Route("api/v1/rbac/[controller]/[action]")]
    [ApiController]
    [CustomAuthorize]
    public class MenuController : ControllerBase
    {
        private readonly haiKanChemistryContext _dbContext;
        private readonly IMapper _mapper;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="mapper"></param>
        public MenuController(haiKanChemistryContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public IActionResult List(MenuRequestPayload payload)
        {
            using (_dbContext)
            {
                var query = _dbContext.SystemMenu.AsQueryable();
                if (!string.IsNullOrEmpty(payload.Kw))
                {
                    query = query.Where(x => x.Name.Contains(payload.Kw.Trim()) || x.Url.Contains(payload.Kw.Trim()));
                }
                if (payload.IsDeleted > CommonEnum.IsDeleted.All)
                {
                    query = query.Where(x => (CommonEnum.IsDeleted)x.IsDeleted == payload.IsDeleted);
                }
                if (payload.Status > CommonEnum.Status.All)
                {
                    query = query.Where(x => (CommonEnum.Status)x.Status == payload.Status);
                }
                if (payload.ParentGuid.HasValue)
                {
                    query = query.Where(x => x.ParentGuid == payload.ParentGuid);
                }
                var list = query.Paged(payload.CurrentPage, payload.PageSize).ToList();
                var totalCount = query.Count();
                var data = list.Select(_mapper.Map<SystemMenu, MenuJsonModel>);
                var response = ResponseModelFactory.CreateResultInstance;
                response.SetData(data, totalCount);
                ToLog.AddLog("??????", "??????:??????:??????????????????????????????", _dbContext);
                return Ok(response);
            }
        }

        /// <summary>
        /// ????????????
        /// </summary>
        /// <param name="model">??????????????????</param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(200)]
        public IActionResult Create(MenuCreateViewModel model)
        {
            using (_dbContext)
            {
                var entity = _mapper.Map<MenuCreateViewModel, SystemMenu>(model);
                entity.CreatedOn = DateTime.Now.ToString("yyyy-MM-dd");
                entity.SystemMenuUuid = Guid.NewGuid();
                entity.CreatedByUserGuid = AuthContextService.CurrentUser.Guid;
                entity.CreatedByUserName = AuthContextService.CurrentUser.DisplayName;
                entity.IsDeleted = 0;
                entity.Icon = string.IsNullOrEmpty(entity.Icon) ? "md-menu" : entity.Icon;
                _dbContext.SystemMenu.Add(entity);
                int res = _dbContext.SaveChanges();
                if (res > 0)
                {
                    ToLog.AddLog("??????", "??????:??????:??????????????????????????????", _dbContext);
                }
                var response = ResponseModelFactory.CreateInstance;
                response.SetSuccess();
                return Ok(response);
            }
        }

        /// <summary>
        /// ????????????
        /// </summary>
        /// <param name="guid">??????ID</param>
        /// <returns></returns>
        [HttpGet("{guid}")]
        [ProducesResponseType(200)]
        public IActionResult Edit(Guid guid)
        {
            using (_dbContext)
            {
                var entity = _dbContext.SystemMenu.FirstOrDefault(x => x.SystemMenuUuid == guid);
                var response = ResponseModelFactory.CreateInstance;
                var model = _mapper.Map<SystemMenu, MenuEditViewModel>(entity);
                //if (model.ParentGuid.HasValue)
                //{
                //    var parent = _dbContext.DncMenu.FirstOrDefault(x => x.Guid == entity.ParentGuid);
                //    if (parent != null)
                //    {
                //        model.ParentName = parent.Name;
                //    }
                //}
                var tree = LoadMenuTree(model.ParentGuid.ToString());
                response.SetData(new { model, tree });
                return Ok(response);
            }
        }

        /// <summary>
        /// ??????????????????????????????
        /// </summary>
        /// <param name="model">??????????????????</param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(200)]
        public IActionResult Edit(MenuEditViewModel model)
        {
            using (_dbContext)
            {
                var entity = _dbContext.SystemMenu.FirstOrDefault(x => x.SystemMenuUuid == model.SystemMenuUuid);
                entity.Name = model.Name;
                entity.Icon = string.IsNullOrEmpty(model.Icon) ? "md-menu" : model.Icon;
                entity.Level = 1;
                entity.ParentGuid = model.ParentGuid;
                entity.Sort = model.Sort;
                entity.Url = model.Url;
                entity.ModifiedByUserGuid = AuthContextService.CurrentUser.Guid;
                entity.ModifiedByUserName = AuthContextService.CurrentUser.DisplayName;
                entity.ModifiedOn = DateTime.Now.ToString("yyyy-MM-dd");
                entity.Description = model.Description;
                entity.ParentName = model.ParentName;
                entity.Component = model.Component;
                entity.HideInMenu = (int)model.HideInMenu;
                entity.NotCache = (int)model.NotCache;
                entity.BeforeCloseFun = model.BeforeCloseFun;
                if (!ConfigurationManager.AppSettings.IsTrialVersion)
                {
                    entity.Alias = model.Alias;
                    entity.IsDeleted = (int)model.IsDeleted;
                    entity.Status = (int)model.Status;
                    entity.IsDefaultRouter = (int)model.IsDefaultRouter;
                }
                int res = _dbContext.SaveChanges();
                if (res > 0)
                {
                    ToLog.AddLog("??????", "??????:??????:??????????????????????????????", _dbContext);
                }
                var response = ResponseModelFactory.CreateInstance;
                response.SetSuccess();
                return Ok(response);
            }
        }

        /// <summary>
        /// ?????????
        /// </summary>
        /// <returns></returns>
        [HttpGet("{selected?}")]
        public IActionResult Tree(string selected)
        {
            var response = ResponseModelFactory.CreateInstance;
            var tree = LoadMenuTree(selected?.ToString());
            response.SetData(tree);
            return Ok(response);
        }

        /// <summary>
        /// ????????????
        /// </summary>
        /// <param name="ids">??????ID,?????????????????????</param>
        /// <returns></returns>
        [HttpGet("{ids}")]
        [ProducesResponseType(200)]
        public IActionResult Delete(string ids)
        {
            var response = ResponseModelFactory.CreateInstance;
            if (ConfigurationManager.AppSettings.IsTrialVersion)
            {
                response.SetIsTrial();
                return Ok(response);
            }
            response = UpdateIsDelete(CommonEnum.IsDeleted.Yes, ids);
            return Ok(response);
        }

        /// <summary>
        /// ????????????
        /// </summary>
        /// <param name="ids">??????ID,?????????????????????</param>
        /// <returns></returns>
        [HttpGet("{ids}")]
        [ProducesResponseType(200)]
        public IActionResult Recover(string ids)
        {
            var response = UpdateIsDelete(CommonEnum.IsDeleted.No, ids);
            return Ok(response);
        }

        /// <summary>
        /// ????????????
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ids">??????ID,?????????????????????</param>
        /// <returns></returns>
        [HttpGet]
        [ProducesResponseType(200)]
        public IActionResult Batch(string command, string ids)
        {
            var response = ResponseModelFactory.CreateInstance;
            switch (command)
            {
                case "delete":
                    if (ConfigurationManager.AppSettings.IsTrialVersion)
                    {
                        response.SetIsTrial();
                        return Ok(response);
                    }
                    response = UpdateIsDelete(CommonEnum.IsDeleted.Yes, ids);
                    break;
                case "recover":
                    response = UpdateIsDelete(CommonEnum.IsDeleted.No, ids);
                    break;
                case "forbidden":
                    if (ConfigurationManager.AppSettings.IsTrialVersion)
                    {
                        response.SetIsTrial();
                        return Ok(response);
                    }
                    response = UpdateStatus(CommonEnum.Status.Forbidden, ids);
                    break;
                case "normal":
                    response = UpdateStatus(CommonEnum.Status.Normal, ids);
                    break;
                default:
                    break;
            }
            return Ok(response);
        }

        /// <summary>
        /// ????????????
        /// </summary>
        /// <param name="isDeleted"></param>
        /// <param name="ids">??????ID?????????,?????????????????????</param>
        /// <returns></returns>
        private ResponseModel UpdateIsDelete(CommonEnum.IsDeleted isDeleted, string ids)
        {
            using (_dbContext)
            {
                var parameters = ids.Split(",").Select((id, index) => new SqlParameter(string.Format("@p{0}", index), id)).ToList();
                var parameterNames = string.Join(", ", parameters.Select(p => p.ParameterName));
                var sql = string.Format("UPDATE SystemMenu SET IsDeleted=@IsDeleted WHERE SystemMenuUUID IN ({0})", parameterNames);
                parameters.Add(new SqlParameter("@IsDeleted", (int)isDeleted));
                _dbContext.Database.ExecuteSqlRaw(sql, parameters);
                ToLog.AddLog("??????", "??????:??????:??????????????????????????????", _dbContext);
                var response = ResponseModelFactory.CreateInstance;
                return response;
            }
        }

        /// <summary>
        /// ????????????
        /// </summary>
        /// <param name="status">????????????</param>
        /// <param name="ids">??????ID?????????,?????????????????????</param>
        /// <returns></returns>
        private ResponseModel UpdateStatus(CommonEnum.Status status, string ids)
        {
            using (_dbContext)
            {
                var parameters = ids.Split(",").Select((id, index) => new SqlParameter(string.Format("@p{0}", index), id)).ToList();
                var parameterNames = string.Join(", ", parameters.Select(p => p.ParameterName));
                var sql = string.Format("UPDATE SystemMenu SET Status=@Status WHERE SystemMenuUUID IN ({0})", parameterNames);
                parameters.Add(new SqlParameter("@Status", (int)status));
                _dbContext.Database.ExecuteSqlRaw(sql, parameters);
                ToLog.AddLog("??????", "??????:??????:??????????????????????????????", _dbContext);
                var response = ResponseModelFactory.CreateInstance;
                return response;
            }
        }

        private List<MenuTree> LoadMenuTree(string selectedGuid = null)
        {
            var temp = _dbContext.SystemMenu.Where(x => x.IsDeleted == (int)CommonEnum.IsDeleted.No && x.Status == (int)CommonEnum.Status.Normal).ToList().Select(x => new MenuTree
            {
                Guid = x.SystemMenuUuid.ToString(),
                ParentGuid = x.ParentGuid,
                Title = x.Name
            }).ToList();
            var root = new MenuTree
            {
                Title = "????????????",
                Guid = Guid.Empty.ToString(),
                ParentGuid = null
            };
            temp.Insert(0, root);
            var tree = temp.BuildTree(selectedGuid);
            return tree;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public static class MenuTreeHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="menus"></param>
        /// <param name="selectedGuid"></param>
        /// <returns></returns>
        public static List<MenuTree> BuildTree(this List<MenuTree> menus, string selectedGuid = null)
        {
            var lookup = menus.ToLookup(x => x.ParentGuid);
            Func<Guid?, List<MenuTree>> build = null;
            build = pid =>
            {
                return lookup[pid]
                 .Select(x => new MenuTree()
                 {
                     Guid = x.Guid,
                     ParentGuid = x.ParentGuid,
                     Title = x.Title,
                     Expand = (x.ParentGuid == null || x.ParentGuid == Guid.Empty),
                     Selected = selectedGuid == x.Guid,
                     Children = build(new Guid(x.Guid)),
                 })
                 .ToList();
            };
            var result = build(null);
            return result;
        }
    }
}