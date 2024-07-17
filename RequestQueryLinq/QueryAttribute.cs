using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RequestQueryLinq
{
    public class QueryAttribute<T> : ActionFilterAttribute
    {

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            var filter = context.HttpContext.Request.Query["$filter"].ToString();
            var sort = context.HttpContext.Request.Query["$sort"].ToString();
            var take = context.HttpContext.Request.Query["$take"].ToString();
            var skip = context.HttpContext.Request.Query["$skip"].ToString();

            var filters = ParseFilters(filter);
            // Применяем фильтры к результату запроса
            (IQueryable result, int count) = ((IQueryable, int))ApplyFilters(context.Result as OkObjectResult, filters, skip, take, sort);

            // Заменяем результат запроса отфильтрованным результатом
            var executedResult = context.Result as ObjectResult;
            executedResult.Value = new { count, result };

        }

        private dynamic ParseFilters(string filter)
        {
            try
            {
                //var regex = new Regex(@"(\w+)\s*(eq|gt|ls|in|con)\s*'([\w.-]+)'");
                //var regex = new Regex(@"(\w+)\s*(eq|gt|ls|contains|in)\s*(\(.+?\)|'[^']*'|[\w.-]+)");
                //var regex = new Regex(@"([\w.]+)\s*(eq|gt|ls|contains|in)\s*(\(.+?\)|'[^']*'|[\w.-]+)");
                //var regex = new Regex(@"([\w./]+)\s*(eq|gt|ls|contains|in)\s*('[^']*'|\(.+?\)|[\w.-]+)");
                //var regex = new Regex(@"([\w./]+)\s*(eq|nq|and|or|gt|ls|contains|in|any)\s*(\([^)]+\)|'[^']*'|[\w.-]+)");
                //var regex = new Regex(@"([\w./]+)\s*(eq|nq|and|or|gt|ls|contains|in|any)\s*(\(.+?\)|'[^']*'|[\w.-]+)");
                //var regex = new Regex(@"([\w./]+)\s*(eq|nq|and|or|gt|gte|ls|lse|contains|ncontains|in|nin|.any)\s*(\([^)]+\)|'[^']*'|[\w.-]+)");
                //var regex = new Regex(@"([\w./]+)\s*(eq|nq|and|or|gt|gte|lt|lte|contains|ncontains|in|nin|any)\s*(\([^)]+\)|'[^']*'|[\w.-]+)");
                var regex = new Regex(@"([\w./]+)\s*(eq|nq|gt|gte|lt|lte|contains|ncontains|in|nin|.any)\s*(\([^)]+\)|'[^']*'|[\w.-]+)|\s*(and|or)\s*");
                var matches = regex.Matches(filter);

                var filters = matches.Select(match =>
                {
                    var field = match.Groups[1].Value;
                    var operatorType = match.Groups[2].Value;
                    var valueString = string.Empty;
                    var orAndValue = match.Groups[0].Value.Trim(' ');

                    object value;
                    if (operatorType == "in" || operatorType == "nin")
                    {
                        valueString = match.Groups[3].Value.Trim('\'', '(', ')');
                        value = valueString.Split(new[] { "', '" }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(v => PropertyHelper.PropertyTypeConvert<T>(field, v.Trim('\'')))
                                           .ToArray();
                    }
                    else if (operatorType == ".any")
                    {
                        valueString = match.Groups[3].Value;
                        var propertyType = PropertyHelper.GetPropertyInfo(typeof(T), field).PropertyType.GetGenericArguments()[0];
                        value = ParseFilters(propertyType, valueString);
                    }
                    else if (orAndValue == "or" || orAndValue == "and")
                    {
                        field = string.Empty;
                        operatorType = orAndValue;
                        value = null;
                    }
                    else
                    {
                        valueString = match.Groups[3].Value.Trim('\'');
                        value = PropertyHelper.PropertyTypeConvert<T>(field, valueString);  // Преобразуем значение в соответствующий тип
                    }

                    return new FieldFilter
                    {
                        Field = field,
                        Operator = operatorType,
                        Value = value
                    };
                }).ToList();

                return filters;
            }
            catch (Exception)
            {

                throw;
            }
        }

        private dynamic ParseFilters(Type type, string filter)
        {
            try
            {
                //var regex = new Regex(@"(\w+)\s*(eq|gt|ls|in|con)\s*'([\w.-]+)'");
                //var regex = new Regex(@"(\w+)\s*(eq|gt|ls|contains|in)\s*(\(.+?\)|'[^']*'|[\w.-]+)");
                //var regex = new Regex(@"([\w.]+)\s*(eq|gt|ls|contains|in)\s*(\(.+?\)|'[^']*'|[\w.-]+)");
                //var regex = new Regex(@"([\w./]+)\s*(eq|gt|ls|contains|in)\s*('[^']*'|\(.+?\)|[\w.-]+)");
                //var regex = new Regex(@"([\w./]+)\s*(eq|nq|and|or|gt|ls|contains|in|any)\s*(\([^)]+\)|'[^']*'|[\w.-]+)");
                //var regex = new Regex(@"([\w./]+)\s*(eq|nq|and|or|gt|ls|contains|in|any)\s*(\(.+?\)|'[^']*'|[\w.-]+)");
                var regex = new Regex(@"([\w./]+)\s*( eq | nq | and | or | gt | gte | lt | lte | contains | ncontains | in | nin |.any)\s*(\([^)]+\)|'[^']*'|[\w.-]+)");
                var matches = regex.Matches(filter);

                var filters = matches.Select(match =>
                {
                    var field = match.Groups[1].Value;
                    var operatorType = match.Groups[2].Value;
                    var valueString = string.Empty;

                    object value;
                    if (operatorType == "in" || operatorType == "nin")
                    {
                        valueString = match.Groups[3].Value.Trim('\'', '(', ')');
                        value = valueString.Split(new[] { "', '" }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(v => PropertyHelper.PropertyTypeConvert(type, field.Split(".")[1], v.Trim('\'')))
                                           .ToArray();
                    }
                    else if (operatorType == ".any")
                    {
                        valueString = match.Groups[3].Value.Trim('(', ')');
                        var propertyType = PropertyHelper.GetPropertyInfo(type, field.Split(".")[1]).PropertyType.GetGenericArguments()[0];
                        value = ParseFilters(propertyType, valueString);
                    }
                    else
                    {
                        valueString = match.Groups[3].Value.Trim('\'');
                        value = PropertyHelper.PropertyTypeConvert(type, field.Split(".")[1], valueString);  // Преобразуем значение в соответствующий тип
                    }

                    return new FieldFilter
                    {
                        Field = field,
                        Operator = operatorType.Trim(),
                        Value = value
                    };
                }).ToList();

                return filters;
            }
            catch (Exception)
            {

                throw;
            }
        }

        private (IQueryable result, int count) ApplyFilters(OkObjectResult result, List<FieldFilter> filters, string skip, string take, string sort)
        {
            var queryable = result.Value as IQueryable<T>;
            int count = queryable.Count();
            if (queryable != null)
            {
                // Применяем сортировку
                var results = queryable.ApplyFilter(filters)
                    .ApplySort(sort);

                if (filters.Count > 0)
                {
                    count = results.Count();
                    results = results
                        .Skip(skip)
                        .Take(take);

                    return (results, count);
                }
                else
                {
                    return (results
                                .Skip(skip)
                                .Take(take), count);
                }
            }
            return (queryable, 0);
        }
    }
}
