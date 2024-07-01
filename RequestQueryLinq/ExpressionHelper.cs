using System.Linq.Expressions;
using System.Reflection;

namespace RequestQueryLinq
{
    public static class ExpressionHelper
    {
        public static IQueryable<T> ApplySort<T>(this IQueryable<T> queryable, string sort)
        {
            if (!string.IsNullOrEmpty(sort))
            {
                var sorts = sort.Split(',')
                    .Select(s => s.Trim().Split(' '))
                    .Select(s => new
                    {
                        Field = s[0],
                        Direction = s[1].ToLower() == "asc" ? nameof(Enumerable.OrderBy) : nameof(Enumerable.OrderByDescending)
                    })
                    .ToList();

                var parameter = Expression.Parameter(typeof(T), "x");
                var orderByExp = queryable.Expression;

                foreach (var sortItem in sorts)
                {
                    var property = Expression.Property(parameter, sortItem.Field);
                    orderByExp = Expression.Call(
                        typeof(Queryable),
                        sortItem.Direction,
                        new[] { typeof(T), property.Type },
                        orderByExp,
                        Expression.Lambda(property, parameter));
                }

                queryable = queryable.Provider.CreateQuery<T>(orderByExp);
                return queryable;
            }
            return queryable;
        }

        //public static int Count<T>(this IQueryable<T> queryable)
        //{
        //    var countExpression = Expression.Call(
        //           typeof(Queryable),
        //           nameof(Enumerable.Count),
        //           new[] { typeof(T) },
        //           queryable.Expression);


        //    var countLambda = Expression.Lambda<Func<int>>(countExpression);
        //    return queryable.Provider.Execute<int>(countExpression);
        //}

        public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> queryable, List<FieldFilter> filters)
        {
            foreach (var filter in filters)
            {
                var field = filter.Field;
                var value = filter.Value;

                // Создаем выражение для фильтрации
                ParameterExpression parameter = Expression.Parameter(typeof(T), "x");
                Expression property = GetPropertyExpression(parameter, field);
                ConstantExpression constant = Expression.Constant(value, value.GetType());
                BinaryExpression comparison;
                LambdaExpression lambda;
                MethodCallExpression methodCallExpression;

                switch (filter.Operator)
                {
                    case "gt":
                        comparison = Expression.GreaterThan(property, constant);
                        break;
                    case "gte":
                        comparison = Expression.GreaterThanOrEqual(property, constant);
                        break;
                    case "lt":
                        comparison = Expression.LessThan(property, constant);
                        break;
                    case "lte":
                        comparison = Expression.LessThanOrEqual(property, constant);
                        break;
                    case "eq":
                        comparison = Expression.Equal(property, constant);
                        break;
                    case "nq":
                        comparison = Expression.NotEqual(property, constant);
                        break;
                    case "and":
                        comparison = Expression.AndAlso(property, constant);
                        break;
                    case "or":
                        comparison = Expression.Or(property, constant);
                        break;
                    case "in":
                        ConstantExpression[] values = ((object[])filter.Value).Select(val => Expression.Constant(val, property.Type)).ToArray();
                        NewArrayExpression constantArray = Expression.NewArrayInit(property.Type, values);
                        MethodInfo inMethod = typeof(Enumerable).GetMethods()
                            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                            .MakeGenericMethod(property.Type);

                        MethodCallExpression containsMethodCall = Expression.Call(inMethod, constantArray, property);

                        lambda = Expression.Lambda(containsMethodCall, parameter);

                        methodCallExpression = Expression.Call(
                            typeof(Queryable),
                            nameof(Enumerable.Where),
                            new[] { typeof(T) },
                            queryable.Expression,
                            lambda);

                        queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                        continue;
                    case "nin":
                        ConstantExpression[] nValues = ((object[])filter.Value).Select(val => Expression.Constant(val, property.Type)).ToArray();
                        NewArrayExpression nConstantArray = Expression.NewArrayInit(property.Type, nValues);
                        MethodInfo ninMethod = typeof(Enumerable).GetMethods()
                            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                            .MakeGenericMethod(property.Type);
                        UnaryExpression nContainsMethodCall = Expression.Not(Expression.Call(ninMethod, nConstantArray, property));

                        lambda = Expression.Lambda(nContainsMethodCall, parameter);

                        methodCallExpression = Expression.Call(
                            typeof(Queryable),
                            nameof(Enumerable.Where),
                            new[] { typeof(T) },
                            queryable.Expression,
                            lambda);

                        queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                        continue;
                    case "contains":
                        if (property.Type == typeof(string))
                        {
                            MethodInfo toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
                            MethodCallExpression propertyToLower = Expression.Call(property, toLowerMethod);
                            MethodCallExpression constantToLower = Expression.Call(constant, toLowerMethod);

                            MethodInfo containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
                            MethodCallExpression containsMethodExp = Expression.Call(propertyToLower, containsMethod, constantToLower);

                            lambda = Expression.Lambda(containsMethodExp, parameter);

                            methodCallExpression = Expression.Call(
                                typeof(Queryable),
                                nameof(Enumerable.Where),
                                new[] { typeof(T) },
                                queryable.Expression,
                                lambda);

                            queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                            continue;
                        }
                        throw new ArgumentException($"The 'contains' operator can only be used with string fields.");
                    case "ncontains":
                        if (property.Type == typeof(string))
                        {
                            MethodInfo nToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
                            MethodCallExpression nPropertyToLower = Expression.Call(property, nToLowerMethod);
                            MethodCallExpression nConstantToLower = Expression.Call(constant, nToLowerMethod);

                            MethodInfo nContainsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
                            UnaryExpression nContainsMethodExp = Expression.Not(Expression.Call(nPropertyToLower, nContainsMethod, nConstantToLower));

                            lambda = Expression.Lambda(nContainsMethodExp, parameter);

                            methodCallExpression = Expression.Call(
                                typeof(Queryable),
                                nameof(Enumerable.Where),
                                new[] { typeof(T) },
                                queryable.Expression,
                                lambda);

                            queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                            continue;
                        }
                        throw new ArgumentException($"The 'ncontains' operator can only be used with string fields.");
                    case ".any":
                        var nestedFilters = (List<FieldFilter>)filter.Value;

                        var nestedParameterType = PropertyHelper.GetPropertyInfo(typeof(T), filter.Field);
                        var nestedParameter = Expression.Parameter(nestedParameterType.PropertyType.GetGenericArguments()[0], "s");

                        MethodInfo anyMethod = typeof(Enumerable).GetMethods()
                            .First(m => m.Name == nameof(Enumerable.Any) && m.GetParameters().Length == 2)
                            .MakeGenericMethod(nestedParameter.Type);

                        Expression nestedPredicate = nestedFilters.Select(nestedFilter => BuildExpression(nestedParameter, nestedFilter))
                                                          .Aggregate<Expression, Expression>(null, (current, expression) =>
                                                              current == null ? expression : Expression.AndAlso(current, expression));

                        var anyPredicate = Expression.Call(
                            anyMethod,
                            Expression.Property(parameter, filter.Field.Split('.')[0]),
                            Expression.Lambda(nestedPredicate, nestedParameter));

                        lambda = Expression.Lambda(anyPredicate, parameter);

                        methodCallExpression = Expression.Call(
                            typeof(Queryable),
                            nameof(Enumerable.Where),
                            new[] { typeof(T) },
                            queryable.Expression,
                            lambda);

                        queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
                        continue;
                    default:
                        continue;
                }

                lambda = Expression.Lambda(comparison, parameter);
                methodCallExpression = Expression.Call(
                    typeof(Queryable),
                    nameof(Enumerable.Where),
                    new[] { typeof(T) },
                    queryable.Expression,
                    lambda);

                queryable = queryable.Provider.CreateQuery<T>(methodCallExpression);
            }

            return queryable;
        }

        public static IQueryable<T> Skip<T>(this IQueryable<T> queryable, string skip)
        {
            if (!string.IsNullOrEmpty(skip) && uint.TryParse(skip, out uint value))
            {
                return queryable.Provider.CreateQuery<T>(
                Expression.Call(
                        typeof(Queryable),
                        nameof(Enumerable.Skip),
                        new[] { typeof(T) },
                        queryable.Expression,
                        Expression.Constant((int)value)));

            }

            return queryable;
        }

        public static IQueryable<T> Take<T>(this IQueryable<T> queryable, string take)
        {
            if (!string.IsNullOrEmpty(take) && uint.TryParse(take, out uint value))
            {
                return queryable.Provider.CreateQuery<T>(
                Expression.Call(
                        typeof(Queryable),
                        nameof(Enumerable.Take),
                        new[] { typeof(T) },
                        queryable.Expression,
                        Expression.Constant((int)value)));
            }

            return queryable;
        }

        private static Expression BuildExpression(ParameterExpression parameter, FieldFilter filter)
        {
            var property = GetNestedPropertyExpression(parameter, filter.Field);
            var constant = Expression.Constant(filter.Value, filter.Value.GetType());
            Expression comparison = null;

            switch (filter.Operator)
            {
                case "gt":
                    comparison = Expression.GreaterThan(property, constant);
                    break;
                case "gte":
                    comparison = Expression.GreaterThanOrEqual(property, constant);
                    break;
                case "lt":
                    comparison = Expression.LessThan(property, constant);
                    break;
                case "lte":
                    comparison = Expression.LessThanOrEqual(property, constant);
                    break;
                case "eq":
                    comparison = Expression.Equal(property, constant);
                    break;
                case "nq":
                    comparison = Expression.NotEqual(property, constant);
                    break;
                case "and":
                    comparison = Expression.AndAlso(property, constant);
                    break;
                case "or":
                    comparison = Expression.Or(property, constant);
                    break;
                case "contains":
                    if (property.Type == typeof(string))
                    {
                        MethodInfo toLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
                        MethodCallExpression propertyToLower = Expression.Call(property, toLowerMethod);
                        MethodCallExpression constantToLower = Expression.Call(constant, toLowerMethod);

                        MethodInfo containsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
                        MethodCallExpression containsMethodExp = Expression.Call(propertyToLower, containsMethod, constantToLower);

                        comparison = containsMethodExp;
                        break;
                    }
                    throw new ArgumentException($"The 'contains' operator can only be used with string fields.");
                case "ncontains":
                    if (property.Type == typeof(string))
                    {
                        MethodInfo nToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes);
                        MethodCallExpression nPropertyToLower = Expression.Call(property, nToLowerMethod);
                        MethodCallExpression nConstantToLower = Expression.Call(constant, nToLowerMethod);

                        MethodInfo nContainsMethod = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
                        UnaryExpression nContainsMethodExp = Expression.Not(Expression.Call(nPropertyToLower, nContainsMethod, nConstantToLower));

                        comparison = nContainsMethodExp;
                        break;
                    }
                    throw new ArgumentException($"The 'contains' operator can only be used with string fields.");
                case "in":
                    ConstantExpression[] values = ((object[])filter.Value).Select(val => Expression.Constant(val, property.Type)).ToArray();
                    NewArrayExpression constantArray = Expression.NewArrayInit(property.Type, values);
                    MethodInfo inMethod = typeof(Enumerable).GetMethods()
                        .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                        .MakeGenericMethod(property.Type);

                    MethodCallExpression containsMethodCall = Expression.Call(inMethod, constantArray, property);
                    comparison = containsMethodCall;
                    break;
                case "nin":
                    ConstantExpression[] nValues = ((object[])filter.Value).Select(val => Expression.Constant(val, property.Type)).ToArray();
                    NewArrayExpression nConstantArray = Expression.NewArrayInit(property.Type, nValues);
                    MethodInfo ninMethod = typeof(Enumerable).GetMethods()
                        .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                        .MakeGenericMethod(property.Type);

                    UnaryExpression nContainsMethodCall = Expression.Not(Expression.Call(ninMethod, nConstantArray, property));
                    comparison = nContainsMethodCall;
                    break;
            }

            return comparison;
        }

        private static Expression GetPropertyExpression(ParameterExpression parameter, string fieldName)
        {
            if (fieldName.Contains('.'))
            {
                // Если название поля содержит точку, это вложенное свойство
                string[] propertyNames = fieldName.Split('.');
                Expression propertyExpression = parameter;

                foreach (var propertyName in propertyNames)
                {
                    propertyExpression = Expression.Property(propertyExpression, propertyName);
                }

                return propertyExpression;
            }
            else
            {
                // Иначе это свойство верхнего уровня
                return Expression.Property(parameter, fieldName);
            }
        }

        private static MemberExpression GetNestedPropertyExpression(Expression parameter, string propertyPath)
        {
            Expression propertyExpression = parameter;
            //foreach (var propertyName in ) // Todo Add nested filter in any 
            //{
            propertyExpression = Expression.Property(propertyExpression, propertyPath.Split('.')[1]);
            //}
            return (MemberExpression)propertyExpression;
        }
    }

    public class FieldFilter
    {
        public string Field { get; set; }
        public string Operator { get; set; }
        public object Value { get; set; }
    }
}
