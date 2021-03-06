﻿using System;
using System.Collections.Generic;
using Autofac;
using JetBrains.Annotations;
using Moq;
using NUnit.Framework;
using Coevery.Caching;
using Coevery.Comments.Handlers;
using Coevery.Comments.Models;
using Coevery.Comments.Services;
using Coevery.ContentManagement;
using Coevery.ContentManagement.Drivers;
using Coevery.ContentManagement.Handlers;
using Coevery.ContentManagement.MetaData;
using Coevery.ContentManagement.Records;
using Coevery.Core.Common.Handlers;
using Coevery.Core.Common.Models;
using Coevery.Data;
using Coevery.DisplayManagement;
using Coevery.DisplayManagement.Descriptors;
using Coevery.DisplayManagement.Implementation;
using Coevery.Environment;
using Coevery.Environment.Extensions;
using Coevery.Security;
using Coevery.Security.Providers;
using Coevery.Services;
using Coevery.Tests.Modules.Users;
using Coevery.Tests.Stubs;
using Coevery.UI.Notify;
using Coevery.UI.PageClass;

namespace Coevery.Tests.Modules.Comments.Services {
    [TestFixture]
    public class CommentServiceTests : DatabaseEnabledTestsBase {
        private IContentManager _contentManager;
        private ICommentService _commentService;

        public override void Register(ContainerBuilder builder) {
            builder.RegisterType<CommentService>().As<ICommentService>();
            builder.RegisterType<DefaultContentManager>().As<IContentManager>();
            builder.RegisterType<StubCacheManager>().As<ICacheManager>();
            builder.RegisterType<Signals>().As<ISignals>();
            builder.RegisterType<DefaultContentManagerSession>().As<IContentManagerSession>();
            builder.RegisterInstance(new Mock<IContentDefinitionManager>().Object);
            builder.RegisterInstance(new Mock<ITransactionManager>().Object);
            builder.RegisterInstance(new Mock<IAuthorizer>().Object);
            builder.RegisterInstance(new Mock<INotifier>().Object);
            builder.RegisterInstance(new Mock<IContentDisplay>().Object);
            builder.RegisterInstance(new Mock<IAuthenticationService>().Object);
            builder.RegisterType<CoeveryServices>().As<ICoeveryServices>();
            builder.RegisterType<DefaultShapeTableManager>().As<IShapeTableManager>();
            builder.RegisterType<DefaultShapeFactory>().As<IShapeFactory>();
            builder.RegisterType<StubWorkContextAccessor>().As<IWorkContextAccessor>();
            builder.RegisterType<CommentedItemHandler>().As<IContentHandler>();
            builder.RegisterType<DefaultContentQuery>().As<IContentQuery>();
            builder.RegisterType<CommentPartHandler>().As<IContentHandler>();
            builder.RegisterType<CommonPartHandler>().As<IContentHandler>();
            builder.RegisterType<StubExtensionManager>().As<IExtensionManager>();
            builder.RegisterType<DefaultEncryptionService>().As<IEncryptionService>();
            builder.RegisterInstance(ShellSettingsUtility.CreateEncryptionEnabled());

            builder.RegisterType<StubClock>().As<IClock>();
            builder.RegisterInstance(new Mock<IPageClassBuilder>().Object); 
            builder.RegisterType<DefaultContentDisplay>().As<IContentDisplay>();
            builder.RegisterGeneric(typeof(Repository<>)).As(typeof(IRepository<>));
        }

        public override void Init() {
            base.Init();
            _commentService = _container.Resolve<ICommentService>();
            _contentManager = _container.Resolve<IContentManager>();
        }


        protected override IEnumerable<Type> DatabaseTypes {
            get {
                return new[] {
                    typeof(CommentsPartRecord),
                    typeof(CommentPartRecord),
                    typeof(ContentItemRecord),
                    typeof(ContentItemVersionRecord),
                    typeof(ContentTypeRecord),
                };
            }
        }

        [Test]
        public void CommentedItemShouldHaveACommentPart() {
            var commentedItem = _contentManager.New("commentedItem");
            _contentManager.Create(commentedItem);
            _contentManager.Create(commentedItem, VersionOptions.Published);

            Assert.That(commentedItem.As<CommentPart>(), Is.Not.Null);
        }

        [Test]
        public void GetCommentsShouldReturnAllComments() {
            for (int i = 0; i < 12; i++) {
                var commentedItem = _contentManager.New("commentedItem");
                _contentManager.Create(commentedItem);
                _contentManager.Create(commentedItem, VersionOptions.Published);

            }

            Assert.That(_commentService.GetComments().Count(), Is.EqualTo(12));
        }

        [Test]
        public void GetCommentedContentShouldReturnCommentedContentItem() {
            var commentedItem = _contentManager.New("commentedItem");
            _contentManager.Create(commentedItem);
            _contentManager.Create(commentedItem, VersionOptions.Published);
            int commentId = commentedItem.As<CommentPart>().Id;

            Assert.That(_commentService.GetCommentedContent(commentId), Is.Not.Null);
        }

        [Test]
        public void CommentsShouldBePendingByDefault() {
            var commentedItem = _contentManager.New("commentedItem");
            _contentManager.Create(commentedItem);
            _contentManager.Create(commentedItem, VersionOptions.Published);
            int commentId = commentedItem.As<CommentPart>().Id;

            Assert.That(_commentService.GetComment(commentId).Record.Status, Is.EqualTo(CommentStatus.Pending));
        }

        [Test]
        public void ApproveShouldUpdateCommentStatus() {
            var commentedItem = _contentManager.New("commentedItem");
            _contentManager.Create(commentedItem);
            _contentManager.Create(commentedItem, VersionOptions.Published);
            int commentId = commentedItem.As<CommentPart>().Id;
            _commentService.ApproveComment(commentId);

            Assert.That(_commentService.GetComment(commentId).Record.Status, Is.EqualTo(CommentStatus.Approved));
        }

        [Test]
        public void UnapproveShouldPendComment() {
            var commentedItem = _contentManager.New("commentedItem");
            _contentManager.Create(commentedItem);
            _contentManager.Create(commentedItem, VersionOptions.Published);
            int commentId = commentedItem.As<CommentPart>().Id;
            _commentService.ApproveComment(commentId);

            Assert.That(_commentService.GetComment(commentId).Record.Status, Is.EqualTo(CommentStatus.Approved));

            _commentService.UnapproveComment(commentId);
            
            Assert.That(_commentService.GetComment(commentId).Record.Status, Is.EqualTo(CommentStatus.Pending));
        }

        [Test]
        public void DeleteShouldRemoveComments() {
            var commentIds = new int[12];

            for (int i = 0; i < 12; i++) {
                var commentedItem = _contentManager.New("commentedItem");
                _contentManager.Create(commentedItem);
                _contentManager.Create(commentedItem, VersionOptions.Published);
                commentIds[i] = commentedItem.As<CommentPart>().Id;
            }

            Assert.That(_commentService.GetComments().Count(), Is.EqualTo(12));

            for (int i = 0; i < 12; i++) {
                _commentService.DeleteComment(commentIds[i]);
            }

            Assert.That(_commentService.GetComments().Count(), Is.EqualTo(0));
        }
    }

    [UsedImplicitly]
    public class CommentedItemHandler : ContentHandler {
        public CommentedItemHandler() {
            Filters.Add(new ActivatingFilter<CommentedItem>("commentedItem"));
            Filters.Add(new ActivatingFilter<CommentPart>("commentedItem"));
            Filters.Add(new ActivatingFilter<CommonPart>("commentedItem"));
        }
    }

    public class CommentedItem : ContentPart {
    }

    public class CommentedItemDriver : ContentPartDriver<CommentedItem> {
        public static readonly string ContentTypeName = "commentedItem";
    }
}
