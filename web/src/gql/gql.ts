/* eslint-disable */
import * as types from './graphql';



/**
 * Map of all GraphQL operations in the project.
 *
 * This map has several performance disadvantages:
 * 1. It is not tree-shakeable, so it will include all operations in the project.
 * 2. It is not minifiable, so the string of a GraphQL query will be multiple times inside the bundle.
 * 3. It does not support dead code elimination, so it will add unused operations.
 *
 * Therefore it is highly recommended to use the babel or swc plugin for production.
 * Learn more about it here: https://the-guild.dev/graphql/codegen/plugins/presets/preset-client#reducing-bundle-size
 */
type Documents = {
    "\n  query Viewer {\n    viewer {\n      email\n      permissions\n    }\n  }\n": typeof types.ViewerDocument,
    "\n  query ContactDetail($id: UUID!) {\n    contact(id: $id) {\n      id\n      name\n      email\n      phone\n      phoneE164\n      street\n      postalCode\n      city\n      country\n      obtainedOn\n      emailOptOut\n      smsOptOut\n      createdAtUtc\n    }\n    contactHistory(contactId: $id, first: 100) {\n      nodes {\n        id\n        rowNumber\n        change\n        changes {\n          field\n          old\n          new\n        }\n        importBatch {\n          id\n          fileName\n          documentId\n          createdAtUtc\n          createdBy\n          category {\n            name\n          }\n        }\n      }\n    }\n  }\n": typeof types.ContactDetailDocument,
    "\n  query Documents {\n    documents(first: 100) {\n      nodes {\n        id\n        fileName\n        contentType\n        sizeBytes\n        status\n        createdAtUtc\n        downloadUrl\n      }\n    }\n  }\n": typeof types.DocumentsDocument,
    "\n  mutation RequestDocumentUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.RequestDocumentUploadDocument,
    "\n  mutation ConfirmDocumentUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      document {\n        id\n        status\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.ConfirmDocumentUploadDocument,
    "\n  mutation RenameDocument($input: RenameDocumentInput!) {\n    renameDocument(input: $input) {\n      document {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.RenameDocumentDocument,
    "\n  mutation ReplaceDocumentContent($input: ReplaceDocumentContentInput!) {\n    replaceDocumentContent(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.ReplaceDocumentContentDocument,
    "\n  mutation DeleteDocument($input: DeleteDocumentInput!) {\n    deleteDocument(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.DeleteDocumentDocument,
    "\n  query ImportCategories {\n    categories {\n      id\n      name\n    }\n  }\n": typeof types.ImportCategoriesDocument,
    "\n  mutation CreateImportCategory($input: CreateCategoryInput!) {\n    createCategory(input: $input) {\n      category {\n        id\n        name\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.CreateImportCategoryDocument,
    "\n  mutation RequestCsvUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.RequestCsvUploadDocument,
    "\n  mutation ConfirmCsvUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.ConfirmCsvUploadDocument,
    "\n  mutation PreviewImport($input: PreviewImportInput!) {\n    previewImport(input: $input) {\n      csvPreview {\n        delimiter\n        encoding\n        headers\n        rows\n        suggestedMapping {\n          name\n          email\n          phone\n          street\n          postalCode\n          city\n          country\n          obtainedOn\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": typeof types.PreviewImportDocument,
    "\n  mutation StartImport($input: StartImportInput!) {\n    startImport(input: $input) {\n      importBatch {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": typeof types.StartImportDocument,
    "\n  query ImportBatches {\n    importBatches(first: 50, order: [{ createdAtUtc: DESC }]) {\n      nodes {\n        id\n        fileName\n        documentId\n        status\n        culture\n        rowCount\n        createdCount\n        updatedCount\n        unchangedCount\n        skippedCount\n        errorCount\n        createdAtUtc\n        createdBy\n        category {\n          name\n        }\n      }\n    }\n  }\n": typeof types.ImportBatchesDocument,
    "\n  query ImportErrors($id: UUID!) {\n    importBatches(where: { id: { eq: $id } }) {\n      nodes {\n        errors {\n          row\n          message\n        }\n      }\n    }\n  }\n": typeof types.ImportErrorsDocument,
    "\n  query Queues {\n    queues {\n      name\n      messages\n      consumers\n    }\n  }\n": typeof types.QueuesDocument,
    "\n  mutation PurgeQueue($input: PurgeQueueInput!) {\n    purgeQueue(input: $input) {\n      unsignedInt\n    }\n  }\n": typeof types.PurgeQueueDocument,
};
const documents: Documents = {
    "\n  query Viewer {\n    viewer {\n      email\n      permissions\n    }\n  }\n": types.ViewerDocument,
    "\n  query ContactDetail($id: UUID!) {\n    contact(id: $id) {\n      id\n      name\n      email\n      phone\n      phoneE164\n      street\n      postalCode\n      city\n      country\n      obtainedOn\n      emailOptOut\n      smsOptOut\n      createdAtUtc\n    }\n    contactHistory(contactId: $id, first: 100) {\n      nodes {\n        id\n        rowNumber\n        change\n        changes {\n          field\n          old\n          new\n        }\n        importBatch {\n          id\n          fileName\n          documentId\n          createdAtUtc\n          createdBy\n          category {\n            name\n          }\n        }\n      }\n    }\n  }\n": types.ContactDetailDocument,
    "\n  query Documents {\n    documents(first: 100) {\n      nodes {\n        id\n        fileName\n        contentType\n        sizeBytes\n        status\n        createdAtUtc\n        downloadUrl\n      }\n    }\n  }\n": types.DocumentsDocument,
    "\n  mutation RequestDocumentUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.RequestDocumentUploadDocument,
    "\n  mutation ConfirmDocumentUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      document {\n        id\n        status\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.ConfirmDocumentUploadDocument,
    "\n  mutation RenameDocument($input: RenameDocumentInput!) {\n    renameDocument(input: $input) {\n      document {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.RenameDocumentDocument,
    "\n  mutation ReplaceDocumentContent($input: ReplaceDocumentContentInput!) {\n    replaceDocumentContent(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.ReplaceDocumentContentDocument,
    "\n  mutation DeleteDocument($input: DeleteDocumentInput!) {\n    deleteDocument(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.DeleteDocumentDocument,
    "\n  query ImportCategories {\n    categories {\n      id\n      name\n    }\n  }\n": types.ImportCategoriesDocument,
    "\n  mutation CreateImportCategory($input: CreateCategoryInput!) {\n    createCategory(input: $input) {\n      category {\n        id\n        name\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.CreateImportCategoryDocument,
    "\n  mutation RequestCsvUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.RequestCsvUploadDocument,
    "\n  mutation ConfirmCsvUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.ConfirmCsvUploadDocument,
    "\n  mutation PreviewImport($input: PreviewImportInput!) {\n    previewImport(input: $input) {\n      csvPreview {\n        delimiter\n        encoding\n        headers\n        rows\n        suggestedMapping {\n          name\n          email\n          phone\n          street\n          postalCode\n          city\n          country\n          obtainedOn\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n": types.PreviewImportDocument,
    "\n  mutation StartImport($input: StartImportInput!) {\n    startImport(input: $input) {\n      importBatch {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n": types.StartImportDocument,
    "\n  query ImportBatches {\n    importBatches(first: 50, order: [{ createdAtUtc: DESC }]) {\n      nodes {\n        id\n        fileName\n        documentId\n        status\n        culture\n        rowCount\n        createdCount\n        updatedCount\n        unchangedCount\n        skippedCount\n        errorCount\n        createdAtUtc\n        createdBy\n        category {\n          name\n        }\n      }\n    }\n  }\n": types.ImportBatchesDocument,
    "\n  query ImportErrors($id: UUID!) {\n    importBatches(where: { id: { eq: $id } }) {\n      nodes {\n        errors {\n          row\n          message\n        }\n      }\n    }\n  }\n": types.ImportErrorsDocument,
    "\n  query Queues {\n    queues {\n      name\n      messages\n      consumers\n    }\n  }\n": types.QueuesDocument,
    "\n  mutation PurgeQueue($input: PurgeQueueInput!) {\n    purgeQueue(input: $input) {\n      unsignedInt\n    }\n  }\n": types.PurgeQueueDocument,
};

/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query Viewer {\n    viewer {\n      email\n      permissions\n    }\n  }\n"): typeof import('./graphql').ViewerDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ContactDetail($id: UUID!) {\n    contact(id: $id) {\n      id\n      name\n      email\n      phone\n      phoneE164\n      street\n      postalCode\n      city\n      country\n      obtainedOn\n      emailOptOut\n      smsOptOut\n      createdAtUtc\n    }\n    contactHistory(contactId: $id, first: 100) {\n      nodes {\n        id\n        rowNumber\n        change\n        changes {\n          field\n          old\n          new\n        }\n        importBatch {\n          id\n          fileName\n          documentId\n          createdAtUtc\n          createdBy\n          category {\n            name\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ContactDetailDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query Documents {\n    documents(first: 100) {\n      nodes {\n        id\n        fileName\n        contentType\n        sizeBytes\n        status\n        createdAtUtc\n        downloadUrl\n      }\n    }\n  }\n"): typeof import('./graphql').DocumentsDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation RequestDocumentUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').RequestDocumentUploadDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation ConfirmDocumentUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      document {\n        id\n        status\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ConfirmDocumentUploadDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation RenameDocument($input: RenameDocumentInput!) {\n    renameDocument(input: $input) {\n      document {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').RenameDocumentDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation ReplaceDocumentContent($input: ReplaceDocumentContentInput!) {\n    replaceDocumentContent(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ReplaceDocumentContentDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation DeleteDocument($input: DeleteDocumentInput!) {\n    deleteDocument(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').DeleteDocumentDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ImportCategories {\n    categories {\n      id\n      name\n    }\n  }\n"): typeof import('./graphql').ImportCategoriesDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation CreateImportCategory($input: CreateCategoryInput!) {\n    createCategory(input: $input) {\n      category {\n        id\n        name\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').CreateImportCategoryDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation RequestCsvUpload($input: RequestDocumentUploadInput!) {\n    requestDocumentUpload(input: $input) {\n      documentUploadPayload {\n        uploadUrl\n        document {\n          id\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').RequestCsvUploadDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation ConfirmCsvUpload($input: ConfirmDocumentUploadInput!) {\n    confirmDocumentUpload(input: $input) {\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ConfirmCsvUploadDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation PreviewImport($input: PreviewImportInput!) {\n    previewImport(input: $input) {\n      csvPreview {\n        delimiter\n        encoding\n        headers\n        rows\n        suggestedMapping {\n          name\n          email\n          phone\n          street\n          postalCode\n          city\n          country\n          obtainedOn\n        }\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').PreviewImportDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation StartImport($input: StartImportInput!) {\n    startImport(input: $input) {\n      importBatch {\n        id\n      }\n      errors {\n        __typename\n        ... on Error {\n          message\n        }\n        ... on ValidationError {\n          fields {\n            field\n            messages\n          }\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').StartImportDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ImportBatches {\n    importBatches(first: 50, order: [{ createdAtUtc: DESC }]) {\n      nodes {\n        id\n        fileName\n        documentId\n        status\n        culture\n        rowCount\n        createdCount\n        updatedCount\n        unchangedCount\n        skippedCount\n        errorCount\n        createdAtUtc\n        createdBy\n        category {\n          name\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ImportBatchesDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query ImportErrors($id: UUID!) {\n    importBatches(where: { id: { eq: $id } }) {\n      nodes {\n        errors {\n          row\n          message\n        }\n      }\n    }\n  }\n"): typeof import('./graphql').ImportErrorsDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  query Queues {\n    queues {\n      name\n      messages\n      consumers\n    }\n  }\n"): typeof import('./graphql').QueuesDocument;
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(source: "\n  mutation PurgeQueue($input: PurgeQueueInput!) {\n    purgeQueue(input: $input) {\n      unsignedInt\n    }\n  }\n"): typeof import('./graphql').PurgeQueueDocument;


export function graphql(source: string) {
  return (documents as any)[source] ?? {};
}
